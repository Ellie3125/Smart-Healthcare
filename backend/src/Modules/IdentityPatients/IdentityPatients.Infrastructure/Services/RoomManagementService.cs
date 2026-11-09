using IdentityPatients.Application.Common;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class RoomManagementService(
    IdentityPatientsDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IRoomManagementService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<ServiceResult<RoomAccountDto>> CreateRoomAccountAsync(
        Guid roomId,
        CreateRoomAccountRequest request,
        CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms.FindAsync([roomId], ct);
        if (room is null)
        {
            return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Không tìm thấy phòng chức năng.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<RoomAccountDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên đăng nhập và mật khẩu không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountCode))
        {
            return ServiceResult<RoomAccountDto>.Failure(400, "Dữ liệu không hợp lệ", "Mã tài khoản (AccountCode) không được để trống.");
        }

        var existingAccount = await _dbContext.RoomAccounts.AnyAsync(r => r.RoomId == roomId, ct);
        if (existingAccount)
        {
            return ServiceResult<RoomAccountDto>.Failure(
                409,
                "Tài khoản phòng đã tồn tại",
                "Phòng chức năng này đã có tài khoản được cấp. Mỗi phòng chỉ có đúng một tài khoản.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return ServiceResult<RoomAccountDto>.Failure(400, "Tạo tài khoản thất bại", errors);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.RoomAccount);
        if (!roleResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<RoomAccountDto>.Failure(400, "Gán quyền thất bại", "Không thể gán vai trò RoomAccount.");
        }

        var roomAccount = new RoomAccount
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            AppUserId = user.Id,
            AccountCode = request.AccountCode.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.RoomAccounts.Add(roomAccount);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            if (IsUniqueViolation(ex, "AccountCode"))
            {
                return ServiceResult<RoomAccountDto>.Failure(409, "Trùng mã tài khoản", "Mã tài khoản phòng đã được sử dụng.");
            }

            if (IsUniqueViolation(ex, "RoomId"))
            {
                return ServiceResult<RoomAccountDto>.Failure(409, "Tài khoản phòng đã tồn tại", "Phòng chức năng này đã có tài khoản được cấp.");
            }

            return ServiceResult<RoomAccountDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo tài khoản phòng do xung đột dữ liệu.");
        }

        var dto = new RoomAccountDto(
            roomAccount.Id,
            roomAccount.RoomId,
            roomAccount.AppUserId,
            roomAccount.AccountCode,
            user.UserName,
            user.Email ?? string.Empty,
            roomAccount.IsActive,
            roomAccount.CreatedAtUtc);

        return ServiceResult<RoomAccountDto>.Success(dto, 201);
    }

    public async Task<ServiceResult<IReadOnlyList<RoomSessionDto>>> GetActiveSessionsAsync(Guid roomId, CancellationToken ct = default)
    {
        var roomAccount = await _dbContext.RoomAccounts.FirstOrDefaultAsync(r => r.RoomId == roomId, ct);
        if (roomAccount is null)
        {
            return ServiceResult<IReadOnlyList<RoomSessionDto>>.Failure(404, "Không tìm thấy", "Phòng này chưa được thiết lập tài khoản.");
        }

        var sessions = await _dbContext.RoomDeviceSessions
            .AsNoTracking()
            .Where(s => s.RoomAccountId == roomAccount.Id && s.IsActive)
            .OrderByDescending(s => s.LoginAtUtc)
            .Select(s => new RoomSessionDto(
                s.Id,
                s.RoomAccountId,
                s.DeviceId,
                s.DeviceIpAddress,
                s.LoginAtUtc,
                s.LogoutAtUtc,
                s.IsActive))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<RoomSessionDto>>.Success(sessions);
    }

    public async Task<ServiceResult<RoomAccountDto>> ToggleRoomAccountActiveAsync(
        Guid roomAccountId,
        bool isActive,
        CancellationToken ct = default)
    {
        var roomAccount = await _dbContext.RoomAccounts.FirstOrDefaultAsync(r => r.Id == roomAccountId, ct);
        if (roomAccount is null)
        {
            return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Không tìm thấy tài khoản phòng.");
        }

        var user = await _userManager.FindByIdAsync(roomAccount.AppUserId.ToString());
        if (user is null)
        {
            return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Không tìm thấy thông tin đăng nhập của phòng.");
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        roomAccount.IsActive = isActive;
        user.IsActive = isActive;

        if (!isActive)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

            // Đóng tất cả phiên hoạt động hiện thời
            var sessions = await _dbContext.RoomDeviceSessions
                .Where(s => s.RoomAccountId == roomAccountId && s.IsActive)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var session in sessions)
            {
                session.IsActive = false;
                session.LogoutAtUtc = now;
            }
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        await _userManager.UpdateAsync(user);
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = new RoomAccountDto(
            roomAccount.Id,
            roomAccount.RoomId,
            roomAccount.AppUserId,
            roomAccount.AccountCode,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            roomAccount.IsActive,
            roomAccount.CreatedAtUtc);

        return ServiceResult<RoomAccountDto>.Success(dto);
    }

    public async Task<bool> ValidateRoomDeviceSessionAsync(Guid roomAccountId, string sessionHash, CancellationToken ct = default)
    {
        return await _dbContext.RoomDeviceSessions
            .AsNoTracking()
            .AnyAsync(s => s.RoomAccountId == roomAccountId && s.SessionToken == sessionHash && s.IsActive, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string keyPart)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            return string.IsNullOrEmpty(keyPart) || sqlEx.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
    }
}
