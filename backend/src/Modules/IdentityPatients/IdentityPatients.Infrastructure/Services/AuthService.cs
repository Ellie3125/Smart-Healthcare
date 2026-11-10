using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class AuthService(
    IdentityPatientsDbContext dbContext,
    ITokenService tokenService,
    SharedDbContext? sharedDbContext = null) : IAuthService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly ITokenService _tokenService = tokenService;
    private readonly SharedDbContext? _sharedDbContext = sharedDbContext;

    public async Task<AuthResult> LoginStaffAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var user = await FindActiveUserAsync(request, ct);
        if (user is null)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Role == Roles.Patient)
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản bệnh nhân không được phép đăng nhập kênh nhân viên.");
        }

        var staffRole = Roles.Staff.Contains(user.Role) ? user.Role : null;
        if (staffRole is null)
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản không có quyền truy cập kênh nhân viên.");
        }

        Guid? doctorId = null;
        if (staffRole == Roles.Doctor)
        {
            doctorId = await (from d in _dbContext.Doctors
                              join s in _dbContext.Staffs on d.StaffId equals s.Id
                              where s.AppUserId == user.Id
                              select (Guid?)d.Id).FirstOrDefaultAsync(ct);
        }

        Guid? roomId = null;
        if (staffRole == Roles.RoomAccount)
        {
            var roomAccount = await _dbContext.RoomAccounts.FirstOrDefaultAsync(r => r.AppUserId == user.Id, ct);
            if (roomAccount != null)
            {
                roomId = roomAccount.RoomId;
                await HandleRoomDeviceSessionAsync(user.Id, roomAccount.Id, request, ct);
            }
        }

        var token = _tokenService.GenerateAccessToken(user, staffRole, Channels.Staff, doctorId, roomId);
        var response = new LoginResponse(token, staffRole, user.UserName ?? string.Empty, user.Id, doctorId, roomId);

        return AuthResult.Success(response);
    }

    public async Task<AuthResult> LoginPatientAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var user = await FindActiveUserAsync(request, ct);
        if (user is null)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Role != Roles.Patient)
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản nhân viên không được phép đăng nhập kênh bệnh nhân.");
        }

        var token = _tokenService.GenerateAccessToken(user, Roles.Patient, Channels.Patient, doctorId: null, roomId: null);
        var response = new LoginResponse(token, Roles.Patient, user.UserName ?? string.Empty, user.Id, null, null);

        return AuthResult.Success(response);
    }

    // Không khóa tài khoản theo số lần sai (ERD v2.4 không có cột, trưởng nhóm chốt 10/11/2026).
    private async Task<AppUser?> FindActiveUserAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);
        // Luôn chạy PBKDF2 để thời gian phản hồi không lộ tên đăng nhập có tồn tại hay không.
        var passwordOk = PasswordHashing.Verify(request.Password, user?.PasswordHash ?? DummyHash.Value);
        return user is { IsActive: true } && passwordOk ? user : null;
    }

    private static readonly Lazy<string> DummyHash = new(() => PasswordHashing.Hash(Guid.NewGuid().ToString()));

    private async Task HandleRoomDeviceSessionAsync(Guid userId, Guid roomAccountId, LoginRequest request, CancellationToken ct)
    {
        var activeSessions = await _dbContext.RoomDeviceSessions
            .Where(s => s.RoomAccountId == roomAccountId && s.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.LogoutAtUtc = now;
        }

        var rawSessionToken = Guid.NewGuid().ToString("N");
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSessionToken))).ToLowerInvariant();

        var newSession = new RoomDeviceSession
        {
            Id = Guid.NewGuid(),
            RoomAccountId = roomAccountId,
            DeviceId = request.DeviceId ?? "default-device",
            DeviceIpAddress = request.DeviceIpAddress ?? "127.0.0.1",
            SessionToken = hashedToken,
            LoginAtUtc = now,
            IsActive = true
        };

        _dbContext.RoomDeviceSessions.Add(newSession);

        if (_sharedDbContext != null && activeSessions.Count > 0)
        {
            _sharedDbContext.Set<AuditLog>().Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = userId,
                RoomAccountId = roomAccountId,
                RoomDeviceSessionId = newSession.Id,
                TableName = "identity.RoomDeviceSession",
                RecordId = newSession.Id,
                ActionType = ActionType.UPDATE,
                DeviceId = newSession.DeviceId,
                ClientIpAddress = newSession.DeviceIpAddress,
                ChangedColumns = "IsActive,LogoutAtUtc",
                ChangeReason = "Ended previous room device sessions upon new device login",
                TimestampUtc = now
            });
            await _sharedDbContext.SaveChangesAsync(ct);
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
