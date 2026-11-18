using System.Net.Mail;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.Data.SqlClient;
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

        string? rawRoomSessionToken = null;
        if (account.RoomAccountId is { } roomAccountId)
        {
            rawRoomSessionToken = await unitOfWork.ExecuteAsync(
                token => StartRoomSessionAsync(user.Id, roomAccountId, request, token), ct);
        }

        var token = rawRoomSessionToken is null
            ? tokenService.GenerateAccessToken(user, account.Role, channel, account.DoctorId, account.RoomId)
            : tokenService.GenerateAccessToken(user, account.Role, channel, account.DoctorId, account.RoomId, rawRoomSessionToken);

        return AuthResult.Success(new LoginResponse(token, account.Role, user.UserName, user.Id, account.DoctorId, account.RoomId));
    }

    // Mỗi RoomAccount tối đa một phiên hoạt động (ERD: Backend kiểm). Khóa hàng RoomAccount để hai lần đăng nhập
    // đồng thời không cùng thấy "chưa có phiên" rồi cùng tạo phiên mới.
    private async Task<string> StartRoomSessionAsync(Guid userId, Guid roomAccountId, LoginRequest request, CancellationToken ct)
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
            DeviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? "default-device" : request.DeviceId.Trim(),
            DeviceIpAddress = string.IsNullOrWhiteSpace(request.DeviceIpAddress) ? "127.0.0.1" : request.DeviceIpAddress.Trim(),
            SessionToken = RoomSessionTokenValidator.HashSessionToken(rawSessionToken),
            LoginAtUtc = now,
            IsActive = true
        };
        dbContext.RoomDeviceSessions.Add(newSession);
        await dbContext.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            PerformedByUserId = userId,
            RoomAccountId = roomAccountId,
            RoomDeviceSessionId = newSession.Id,
            TableName = "identity.RoomDeviceSession",
            RecordId = newSession.Id,
            ActionType = ActionType.INSERT,
            DeviceId = newSession.DeviceId,
            ClientIpAddress = newSession.DeviceIpAddress,
            ChangedColumns = activeSessions.Count > 0 ? "IsActive,LogoutAtUtc,SessionToken" : "SessionToken,IsActive",
            ChangeReason = activeSessions.Count > 0
                ? "Ended previous room device sessions upon new device login"
                : "Started new room device session",
            TimestampUtc = now
        }, ct);

        return rawSessionToken;
    }

    public async Task<ServiceResult> LogoutRoomAsync(Guid userId, string? rawSessionToken, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var roomAccount = await dbContext.RoomAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.AppUserId == userId, token);

            if (roomAccount is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy tài khoản phòng.");
            }

            var sessionHash = string.IsNullOrWhiteSpace(rawSessionToken)
                ? null
                : RoomSessionTokenValidator.HashSessionToken(rawSessionToken);

            var activeQuery = dbContext.RoomDeviceSessions
                .Where(s => s.RoomAccountId == roomAccount.Id && s.IsActive);

            if (sessionHash is not null)
            {
                activeQuery = activeQuery.Where(s => s.SessionToken == sessionHash);
            }

            var sessionsToClose = await activeQuery.ToListAsync(token);
            if (sessionsToClose.Count == 0)
            {
                return ServiceResult.Success();
            }

            var now = DateTime.UtcNow;
            foreach (var session in sessionsToClose)
            {
                session.IsActive = false;
                session.LogoutAtUtc = now;
            }

            await dbContext.SaveChangesAsync(token);

            var targetSession = sessionsToClose[0];
            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = userId,
                RoomAccountId = roomAccount.Id,
                RoomDeviceSessionId = targetSession.Id,
                TableName = "identity.RoomDeviceSession",
                RecordId = targetSession.Id,
                ActionType = ActionType.UPDATE,
                DeviceId = targetSession.DeviceId,
                ClientIpAddress = targetSession.DeviceIpAddress,
                ChangedColumns = "IsActive,LogoutAtUtc",
                ChangeReason = "Tài khoản phòng chủ động đăng xuất",
                TimestampUtc = now
            }, token);

            return ServiceResult.Success();
        }, ct);
    }

    public async Task<AuthResult> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length > 200)
        {
            return AuthResult.Failure(400, "Dữ liệu không hợp lệ", "Tên đăng nhập không hợp lệ (bắt buộc, tối đa 200 ký tự).", "InvalidUserName");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < PasswordHashing.MinPasswordLength)
        {
            return AuthResult.Failure(400, "Dữ liệu không hợp lệ", $"Mật khẩu phải có ít nhất {PasswordHashing.MinPasswordLength} ký tự.", "InvalidPassword");
        }

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (normalizedEmail is not null && (normalizedEmail.Length > 256 || !MailAddress.TryCreate(normalizedEmail, out _)))
        {
            return AuthResult.Failure(400, "Dữ liệu không hợp lệ", "Địa chỉ email không đúng định dạng.", "InvalidEmail");
        }

        var (normalizedPhone, phoneError) = PatientInputValidator.ValidateVietnamesePhone(request.PhoneNumber, required: false);
        if (phoneError is { } phoneErr)
        {
            return AuthResult.Failure(400, phoneErr.Title, phoneErr.Detail, phoneErr.Code);
        }

        if (normalizedEmail is null && normalizedPhone is null)
        {
            return AuthResult.Failure(400, "Dữ liệu không hợp lệ", "Vui lòng cung cấp email hoặc số điện thoại để đăng ký.", "MissingContact");
        }

        // Kiểm tra trùng UserName, Email hoặc PhoneNumber
        var duplicateUser = await dbContext.AppUsers.AsNoTracking()
            .AnyAsync(u => u.UserName == normalizedUserName
                           || (normalizedEmail != null && u.Email == normalizedEmail)
                           || (normalizedPhone != null && u.PhoneNumber == normalizedPhone), ct);

        if (duplicateUser)
        {
            return AuthResult.Failure(409, "Xung đột dữ liệu", "Tên đăng nhập, email hoặc số điện thoại đã được đăng ký.", "DuplicateUser");
        }

        Patient? patient = null;
        PatientRepresentative? representative = null;

        if (request.CreateSelfProfile && request.SelfProfile is { } self)
        {
            var effectivePhone = string.IsNullOrWhiteSpace(self.PhoneNumber) ? normalizedPhone : self.PhoneNumber;
            var (fields, fieldError) = PatientInputValidator.ValidatePatientFields(
                self.FullName,
                self.DateOfBirth,
                self.Gender,
                self.NationalId,
                effectivePhone,
                self.Address);

            if (fieldError is { } err)
            {
                return AuthResult.Failure(400, err.Title, err.Detail, err.Code);
            }

            var validated = fields!;
            if (validated.NationalId is not null)
            {
                var cccdExists = await dbContext.Patients.AsNoTracking().AnyAsync(p => p.NationalId == validated.NationalId, ct);
                if (cccdExists)
                {
                    return AuthResult.Failure(409, "Xung đột dữ liệu", PatientInputValidator.DuplicateNationalIdMessage, PatientProfileService.DuplicateNationalIdCode);
                }
            }

            var now = DateTime.UtcNow;
            patient = new Patient
            {
                Id = Guid.NewGuid(),
                FullName = validated.FullName,
                DateOfBirth = validated.DateOfBirth,
                Gender = validated.Gender,
                NationalId = validated.NationalId,
                PhoneNumber = validated.PhoneNumber,
                Address = validated.Address,
                CreatedAtUtc = now
            };
        }

        var newUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedUserName,
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            PasswordHash = PasswordHashing.Hash(request.Password),
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (patient is not null)
        {
            representative = new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = newUser.Id,
                PatientId = patient.Id,
                Relationship = Relationship.Self,
                VerificationStatus = VerificationStatus.Pending,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                dbContext.AppUsers.Add(newUser);
                if (patient is not null && representative is not null)
                {
                    dbContext.Patients.Add(patient);
                    dbContext.PatientRepresentatives.Add(representative);
                }
                await dbContext.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return AuthResult.Failure(409, "Xung đột dữ liệu", PatientInputValidator.DuplicateNationalIdMessage, PatientProfileService.DuplicateNationalIdCode);
            }

            return AuthResult.Failure(409, "Xung đột dữ liệu", "Tên đăng nhập, email hoặc số điện thoại đã được đăng ký.", "DuplicateUser");
        }

        var accessToken = tokenService.GenerateAccessToken(newUser, Roles.Patient, Channels.Patient, null, null);
        return AuthResult.Success(new LoginResponse(accessToken, Roles.Patient, newUser.UserName, newUser.Id, null, null));
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
