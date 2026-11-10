using IdentityPatients.Application.Common;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;

namespace IdentityPatients.Infrastructure.Services;

public sealed class RoomManagementService(IdentityPatientsDbContext dbContext, IUnitOfWork unitOfWork) : IRoomManagementService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

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

        if (request.Password.Length < PasswordHashing.MinPasswordLength)
        {
            return ServiceResult<RoomAccountDto>.Failure(400, "Dữ liệu không hợp lệ", $"Mật khẩu phải có ít nhất {PasswordHashing.MinPasswordLength} ký tự.");
        }

        var existingAccount = await _dbContext.RoomAccounts.AnyAsync(r => r.RoomId == roomId, ct);
        if (existingAccount)
        {
            return ServiceResult<RoomAccountDto>.Failure(
                409,
                "Tài khoản phòng đã tồn tại",
                "Phòng chức năng này đã có tài khoản được cấp. Mỗi phòng chỉ có đúng một tài khoản.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            PasswordHash = PasswordHashing.Hash(request.Password),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var roomAccount = new RoomAccount
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            AppUserId = user.Id,
            AccountCode = request.AccountCode.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            // AppUser và RoomAccount ghi cùng một unit of work; Add nằm trong work để lần chạy lại sau deadlock vẫn đủ.
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.AppUsers.Add(user);
                _dbContext.RoomAccounts.Add(roomAccount);
                await _dbContext.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "UserName") || IsUniqueViolation(ex, "Email"))
            {
                return ServiceResult<RoomAccountDto>.Failure(409, "Trùng tài khoản", "Tên đăng nhập hoặc email đã được dùng cho tài khoản khác.");
            }

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
        // RoomAccount, AppUser và các phiên đổi cùng một unit of work. Khóa hàng RoomAccount như lúc đăng nhập
        // để một lần đăng nhập đồng thời không tạo phiên mới sau khi tài khoản đã bị tắt.
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var locked = await _dbContext.RoomAccounts
                .FromSql($"SELECT * FROM [identity].[RoomAccount] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roomAccountId}")
                .AsNoTracking()
                .AnyAsync(token);
            var roomAccount = locked ? await _dbContext.RoomAccounts.FindAsync([roomAccountId], token) : null;
            if (roomAccount is null)
            {
                return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Không tìm thấy tài khoản phòng.");
            }

            var user = await _dbContext.AppUsers.FindAsync([roomAccount.AppUserId], token);
            if (user is null)
            {
                return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Không tìm thấy thông tin đăng nhập của phòng.");
            }

            roomAccount.IsActive = isActive;
            user.IsActive = isActive;

            if (!isActive)
            {
                var sessions = await _dbContext.RoomDeviceSessions
                    .Where(s => s.RoomAccountId == roomAccountId && s.IsActive)
                    .ToListAsync(token);

                var now = DateTime.UtcNow;
                foreach (var session in sessions)
                {
                    session.IsActive = false;
                    session.LogoutAtUtc = now;
                }
            }

            await _dbContext.SaveChangesAsync(token);

            return ServiceResult<RoomAccountDto>.Success(new RoomAccountDto(
                roomAccount.Id,
                roomAccount.RoomId,
                roomAccount.AppUserId,
                roomAccount.AccountCode,
                user.UserName,
                user.Email ?? string.Empty,
                roomAccount.IsActive,
                roomAccount.CreatedAtUtc));
        }, ct);
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
