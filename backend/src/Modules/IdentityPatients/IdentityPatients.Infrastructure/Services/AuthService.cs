using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class AuthService(
    IdentityPatientsDbContext dbContext,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    AuditLogWriter auditLog) : IAuthService
{
    public const string InvalidCredentialsCode = "InvalidCredentials";

    // Sai kênh, sai mật khẩu, tài khoản không tồn tại, bị vô hiệu hóa hay Technician đều trả cùng một lỗi (D8).
    private static AuthResult InvalidCredentials() =>
        AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.", InvalidCredentialsCode);

    private static readonly Lazy<string> DummyHash = new(() => PasswordHashing.Hash(Guid.NewGuid().ToString()));

    public Task<AuthResult> LoginStaffAsync(LoginRequest request, CancellationToken ct = default) =>
        LoginAsync(request, Channels.Staff, ct);

    public Task<AuthResult> LoginPatientAsync(LoginRequest request, CancellationToken ct = default) =>
        LoginAsync(request, Channels.Patient, ct);

    private async Task<AuthResult> LoginAsync(LoginRequest request, string channel, CancellationToken ct)
    {
        var userName = request.UserName ?? string.Empty;
        var user = await dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == userName, ct);
        // Luôn chạy PBKDF2 để thời gian phản hồi không lộ tên đăng nhập có tồn tại hay không.
        var passwordOk = PasswordHashing.Verify(request.Password ?? string.Empty, user?.PasswordHash ?? DummyHash.Value);
        if (user is not { IsActive: true } || !passwordOk)
        {
            return InvalidCredentials();
        }

        var account = await AccountRoles.ResolveAsync(dbContext, user.Id, ct);
        if (account is null || account.Channel != channel)
        {
            return InvalidCredentials();
        }

        if (account.RoomAccountId is { } roomAccountId)
        {
            await unitOfWork.ExecuteAsync(token => StartRoomSessionAsync(user.Id, roomAccountId, request, token), ct);
        }

        var token = tokenService.GenerateAccessToken(user, account.Role, channel, account.DoctorId, account.RoomId);
        return AuthResult.Success(new LoginResponse(token, account.Role, user.UserName, user.Id, account.DoctorId, account.RoomId));
    }

    // Mỗi RoomAccount tối đa một phiên hoạt động (ERD: Backend kiểm). Khóa hàng RoomAccount để hai lần đăng nhập
    // đồng thời không cùng thấy "chưa có phiên" rồi cùng tạo phiên mới.
    private async Task StartRoomSessionAsync(Guid userId, Guid roomAccountId, LoginRequest request, CancellationToken ct)
    {
        await dbContext.RoomAccounts
            .FromSql($"SELECT * FROM [identity].[RoomAccount] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roomAccountId}")
            .AsNoTracking()
            .SingleAsync(ct);

        var activeSessions = await dbContext.RoomDeviceSessions
            .Where(s => s.RoomAccountId == roomAccountId && s.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.LogoutAtUtc = now;
        }

        var rawSessionToken = Guid.NewGuid().ToString("N");
        var newSession = new RoomDeviceSession
        {
            Id = Guid.NewGuid(),
            RoomAccountId = roomAccountId,
            DeviceId = request.DeviceId ?? "default-device",
            DeviceIpAddress = request.DeviceIpAddress ?? "127.0.0.1",
            SessionToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSessionToken))).ToLowerInvariant(),
            LoginAtUtc = now,
            IsActive = true
        };
        dbContext.RoomDeviceSessions.Add(newSession);
        await dbContext.SaveChangesAsync(ct);

        if (activeSessions.Count > 0)
        {
            await auditLog.WriteAsync(new AuditLog
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
            }, ct);
        }
    }
}
