using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using IdentityPatients.Application.Common;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class RoomManagementService(
    IdentityPatientsDbContext dbContext,
    IUnitOfWork unitOfWork,
    AuditLogWriter auditLog,
    IHttpContextAccessor? httpContextAccessor = null) : IRoomManagementService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    private Guid? CurrentActorUserId()
    {
        var user = httpContextAccessor?.HttpContext?.User;
        var sub = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

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

        if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
        {
            return ServiceResult<RoomAccountDto>.Failure(400, "Dữ liệu không hợp lệ", "Email phòng không hợp lệ hoặc bị bỏ trống.");
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

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            PasswordHash = PasswordHashing.Hash(request.Password),
            Email = request.Email.Trim().ToLowerInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = now
        };

        var roomAccount = new RoomAccount
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            AppUserId = user.Id,
            AccountCode = request.AccountCode.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = now
        };

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.AppUsers.Add(user);
                _dbContext.RoomAccounts.Add(roomAccount);
                await _dbContext.SaveChangesAsync(token);

                await auditLog.WriteAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    PerformedByUserId = CurrentActorUserId(),
                    RoomAccountId = roomAccount.Id,
                    TableName = "identity.RoomAccount",
                    RecordId = roomAccount.Id,
                    ActionType = ActionType.INSERT,
                    ChangedColumns = "RoomId,AppUserId,AccountCode,IsActive",
                    NewValuesJson = JsonSerializer.Serialize(new { roomAccount.RoomId, roomAccount.AccountCode, user.UserName, user.Email, roomAccount.IsActive }),
                    ChangeReason = $"Admin tạo tài khoản phòng {roomAccount.AccountCode}",
                    TimestampUtc = now
                }, token);
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

        return ServiceResult<RoomAccountDto>.Success(MapDto(roomAccount, user), 201);
    }

    public async Task<ServiceResult<RoomAccountDto>> GetRoomAccountByRoomIdAsync(Guid roomId, CancellationToken ct = default)
    {
        var dto = await (from ra in _dbContext.RoomAccounts.AsNoTracking()
                         join u in _dbContext.AppUsers.AsNoTracking() on ra.AppUserId equals u.Id
                         where ra.RoomId == roomId
                         select new RoomAccountDto(
                             ra.Id,
                             ra.RoomId,
                             ra.AppUserId,
                             ra.AccountCode,
                             u.UserName,
                             u.Email ?? string.Empty,
                             ra.IsActive,
                             ra.CreatedAtUtc)).FirstOrDefaultAsync(ct);

        if (dto is null)
        {
            return ServiceResult<RoomAccountDto>.Failure(404, "Không tìm thấy", "Phòng này chưa được thiết lập tài khoản.");
        }

        return ServiceResult<RoomAccountDto>.Success(dto);
    }

    public Task<ServiceResult<IReadOnlyList<RoomSessionDto>>> GetActiveSessionsAsync(Guid roomId, CancellationToken ct = default) =>
        GetSessionsByRoomAsync(roomId, activeOnly: true, ct);

    public async Task<ServiceResult<IReadOnlyList<RoomSessionDto>>> GetSessionsByRoomAsync(
        Guid roomId,
        bool activeOnly = false,
        CancellationToken ct = default)
    {
        var roomAccount = await _dbContext.RoomAccounts.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == roomId, ct);
        if (roomAccount is null)
        {
            return ServiceResult<IReadOnlyList<RoomSessionDto>>.Failure(404, "Không tìm thấy", "Phòng này chưa được thiết lập tài khoản.");
        }

        var query = _dbContext.RoomDeviceSessions
            .AsNoTracking()
            .Where(s => s.RoomAccountId == roomAccount.Id);

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        var sessions = await query
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.LoginAtUtc)
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

            var oldActive = roomAccount.IsActive;
            roomAccount.IsActive = isActive;
            user.IsActive = isActive;

            var now = DateTime.UtcNow;
            if (!isActive)
            {
                var sessions = await _dbContext.RoomDeviceSessions
                    .Where(s => s.RoomAccountId == roomAccountId && s.IsActive)
                    .ToListAsync(token);

                foreach (var session in sessions)
                {
                    session.IsActive = false;
                    session.LogoutAtUtc = now;
                }
            }

            await _dbContext.SaveChangesAsync(token);

            if (oldActive != isActive)
            {
                await auditLog.WriteAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    PerformedByUserId = CurrentActorUserId(),
                    RoomAccountId = roomAccount.Id,
                    TableName = "identity.RoomAccount",
                    RecordId = roomAccount.Id,
                    ActionType = ActionType.UPDATE,
                    ChangedColumns = "IsActive",
                    OldValuesJson = JsonSerializer.Serialize(new { IsActive = oldActive }),
                    NewValuesJson = JsonSerializer.Serialize(new { IsActive = isActive }),
                    ChangeReason = isActive
                        ? $"Admin kích hoạt tài khoản phòng {roomAccount.AccountCode}"
                        : $"Admin vô hiệu hóa tài khoản phòng {roomAccount.AccountCode}",
                    TimestampUtc = now
                }, token);
            }

            return ServiceResult<RoomAccountDto>.Success(MapDto(roomAccount, user));
        }, ct);
    }

    public async Task<ServiceResult> ResetRoomAccountPasswordAsync(
        Guid actorUserId,
        Guid roomAccountId,
        ResetRoomPasswordRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < PasswordHashing.MinPasswordLength)
        {
            return ServiceResult.Failure(400, "Dữ liệu không hợp lệ", $"Mật khẩu phải có ít nhất {PasswordHashing.MinPasswordLength} ký tự.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var roomAccount = await _dbContext.RoomAccounts.FindAsync([roomAccountId], token);
            if (roomAccount is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy tài khoản phòng.");
            }

            var user = await _dbContext.AppUsers.FindAsync([roomAccount.AppUserId], token);
            if (user is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy thông tin đăng nhập của phòng.");
            }

            var now = DateTime.UtcNow;
            user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
            await _dbContext.SaveChangesAsync(token);

            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = actorUserId,
                RoomAccountId = roomAccount.Id,
                TableName = "identity.AppUser",
                RecordId = user.Id,
                ActionType = ActionType.UPDATE,
                ChangedColumns = "PasswordHash",
                ChangeReason = $"Admin đặt lại mật khẩu tài khoản phòng {roomAccount.AccountCode}",
                TimestampUtc = now
            }, token);

            return ServiceResult.Success();
        }, ct);
    }

    public async Task<ServiceResult> RevokeSessionAsync(Guid actorUserId, Guid sessionId, string reason, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await _dbContext.RoomDeviceSessions.FindAsync([sessionId], token);
            if (session is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy phiên thiết bị phòng.");
            }

            if (!session.IsActive)
            {
                return ServiceResult.Success();
            }

            var now = DateTime.UtcNow;
            session.IsActive = false;
            session.LogoutAtUtc = now;

            await _dbContext.SaveChangesAsync(token);

            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = actorUserId,
                RoomAccountId = session.RoomAccountId,
                RoomDeviceSessionId = session.Id,
                TableName = "identity.RoomDeviceSession",
                RecordId = session.Id,
                ActionType = ActionType.UPDATE,
                DeviceId = session.DeviceId,
                ClientIpAddress = session.DeviceIpAddress,
                ChangedColumns = "IsActive,LogoutAtUtc",
                ChangeReason = string.IsNullOrWhiteSpace(reason) ? "Admin buộc đăng xuất phiên thiết bị phòng" : reason.Trim(),
                TimestampUtc = now
            }, token);

            return ServiceResult.Success();
        }, ct);
    }

    public async Task<bool> ValidateRoomDeviceSessionAsync(Guid roomAccountId, string sessionHash, CancellationToken ct = default)
    {
        return await _dbContext.RoomDeviceSessions
            .AsNoTracking()
            .AnyAsync(s => s.RoomAccountId == roomAccountId && s.SessionToken == sessionHash && s.IsActive, ct);
    }

    private static RoomAccountDto MapDto(RoomAccount roomAccount, AppUser user) => new(
        roomAccount.Id,
        roomAccount.RoomId,
        roomAccount.AppUserId,
        roomAccount.AccountCode,
        user.UserName,
        user.Email ?? string.Empty,
        roomAccount.IsActive,
        roomAccount.CreatedAtUtc);

    private static bool IsUniqueViolation(DbUpdateException ex, string keyPart)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            return string.IsNullOrEmpty(keyPart) || sqlEx.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
    }
}
