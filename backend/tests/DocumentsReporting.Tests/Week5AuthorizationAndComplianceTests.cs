using System.Security.Claims;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Contracts;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class Week5AuthorizationAndComplianceTests
{
    private readonly TestResultAttachmentStore _store = new();
    private readonly InMemoryObjectStorage _storage = new();
    private readonly TestSchedulingModule _scheduling = new();
    private readonly TestClinicalModule _clinical = new();
    private readonly TestIdentityPatientsModule _identity = new();
    private readonly TestAuditLogPort _auditLog = new();
    private readonly TestTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private DocumentsService CreateService() =>
        new(_store, _storage, _scheduling, _clinical, _identity, _auditLog, _clock);

    private static IAuthorizationService BuildAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (policy, role) in Policies.RoleOf)
            {
                options.AddPolicy(policy, p => p.RequireRole(role).RequireClaim(AppClaims.Channel, Policies.ChannelOf(role)));
            }

            options.AddPolicy(Policies.AnyChannel, p => p.RequireAssertion(context =>
                Policies.RoleOf.Values.Any(role => context.User.IsInRole(role)
                    && context.User.HasClaim(AppClaims.Channel, Policies.ChannelOf(role)))));

            options.AddPolicy(Policies.ReceptionistOrAdmin, p => p.RequireAssertion(context =>
                (context.User.IsInRole(Roles.Receptionist) || context.User.IsInRole(Roles.Admin))
                && context.User.HasClaim(AppClaims.Channel, Channels.Staff)));
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreatePrincipal(string role, string channel, string? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new(AppClaims.Channel, channel),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // =========================================================================
    // 1. KÊNH & VAI TRÒ: TOKEN PATIENT BỊ TỪ CHỐI BỞI CÁC POLICY NHÂN VIÊN
    // =========================================================================

    [Theory]
    [InlineData(Policies.AdminOnly)]
    [InlineData(Policies.DoctorOnly)]
    [InlineData(Policies.NurseOnly)]
    [InlineData(Policies.ReceptionistOnly)]
    [InlineData(Policies.RoomAccountOnly)]
    [InlineData(Policies.ReceptionistOrAdmin)]
    public async Task PatientToken_RejectedByAllStaffPolicies(string staffPolicy)
    {
        var authService = BuildAuthorizationService();
        var patientPrincipal = CreatePrincipal(Roles.Patient, Channels.Patient);

        var result = await authService.AuthorizeAsync(patientPrincipal, null, staffPolicy);
        Assert.False(result.Succeeded, $"Patient token must be rejected by {staffPolicy}");
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Nurse)]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.RoomAccount)]
    public async Task StaffToken_RejectedByPatientOnlyPolicy(string staffRole)
    {
        var authService = BuildAuthorizationService();
        var staffPrincipal = CreatePrincipal(staffRole, Channels.Staff);

        var result = await authService.AuthorizeAsync(staffPrincipal, null, Policies.PatientOnly);
        Assert.False(result.Succeeded, $"Staff role {staffRole} must be rejected by PatientOnly policy");
    }

    [Theory]
    [InlineData(Roles.Patient, Channels.Staff, Policies.PatientOnly)] // Patient giả mạo channel Staff
    [InlineData(Roles.Admin, Channels.Patient, Policies.AdminOnly)]     // Admin đăng nhập channel Patient
    [InlineData(Roles.Doctor, Channels.Patient, Policies.DoctorOnly)]   // Doctor đăng nhập channel Patient
    [InlineData(Roles.RoomAccount, Channels.Patient, Policies.RoomAccountOnly)]
    public async Task MismatchedChannelAndRole_RejectedByBackendPolicies(string role, string channel, string policy)
    {
        var authService = BuildAuthorizationService();
        var principal = CreatePrincipal(role, channel);

        var result = await authService.AuthorizeAsync(principal, null, policy);
        Assert.False(result.Succeeded, $"Mismatched {role} on {channel} must be rejected by {policy}");
    }

    // =========================================================================
    // 2. PHÒNG KHÁM & TICKET CLS: SAI ROOM_ID BỊ TỪ CHỐI
    // =========================================================================

    [Fact]
    public async Task RoomAccount_UploadWithMismatchedRoomId_ThrowsTicketRoomMismatch()
    {
        var service = CreateService();
        var assignedRoomId = Guid.NewGuid();
        var wrongRoomId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            QueueTicketId: tId,
            RoomId: assignedRoomId,
            EncounterId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            ServiceOrderItemId: Guid.NewGuid(),
            CLSResultId: Guid.NewGuid(),
            TicketNumber: "CLS-001",
            PatientName: "Bệnh nhân mẫu",
            PatientDateOfBirth: new DateOnly(1990, 1, 1),
            IsServiceFinished: true,
            IsResultReturned: false));

        using var stream = new MemoryStream("%PDF-1.7\nSample"u8.ToArray());
        var command = new UploadResultFile(ticketId, wrongRoomId, "result.pdf", "application/pdf", stream);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(command));
        Assert.Equal(DocumentsErrorCode.TicketRoomMismatch, ex.ErrorCode);
    }

    // =========================================================================
    // 3. TỆP CHƯA PHÁT HÀNH: BỆNH NHÂN TẢI BỊ TỪ CHỐI
    // =========================================================================

    [Fact]
    public async Task Patient_DownloadUnpublishedResultFile_ThrowsResultNotPublished()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = new MemoryStream("%PDF-1.7\nSample"u8.ToArray());
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "draft.pdf", "application/pdf", stream));

        // Tệp vẫn ở trạng thái Draft (chưa gọi MarkPublishedAsync)
        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(true);
        _identity.CanAccessHandler = (_, _, _) => Task.FromResult(true);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.ResultNotPublished, ex.ErrorCode);
    }

    // =========================================================================
    // 4. HỒ SƠ PENDING/REJECTED: XEM KẾT QUẢ/LỊCH SỬ BỊ TỪ CHỐI
    // =========================================================================

    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    public async Task Patient_PendingOrRejectedProfile_DeniedAccessToClinicalRecords(string verificationStatus)
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = new MemoryStream("%PDF-1.7\nSample"u8.ToArray());
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "result.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(true);

        // Mô phỏng IdentityPatients từ chối quyền ClinicalRecords khi hồ sơ không phải Verified
        _identity.CanAccessHandler = (_, _, level) =>
        {
            if (level == AccessLevel.ClinicalRecords)
            {
                return Task.FromResult(verificationStatus == "Verified");
            }
            return Task.FromResult(true);
        };

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.AccessDenied, ex.ErrorCode);
    }
}
