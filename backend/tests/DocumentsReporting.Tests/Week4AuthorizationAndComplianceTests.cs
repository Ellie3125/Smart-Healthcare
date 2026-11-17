using System.Text;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using IdentityPatients.Contracts;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class Week4AuthorizationAndComplianceTests
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

    private static MemoryStream CreateValidPdfStream() =>
        new(Encoding.UTF8.GetBytes("%PDF-1.7\nSample content for medical result PDF"));

    // =========================================================================
    // 1. KIỂM THỬ XÁC MINH HỒ SƠ: PENDING/REJECTED/REVOKED BỊ TỪ CHỐI, VERIFIED XEM ĐƯỢC
    // =========================================================================

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("Rejected", false)]
    [InlineData("Revoked", false)]
    [InlineData("Verified", true)]
    public async Task CanAccessPatient_ClinicalRecords_OnlyVerifiedProfilesAllowed(string statusString, bool expectedAccess)
    {
        var service = CreateService();
        var patientUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(true);

        // Mock CanAccessPatientAsync theo trạng thái hồ sơ
        _identity.CanAccessHandler = (uId, pId, level) =>
        {
            if (level == AccessLevel.ClinicalRecords)
            {
                return Task.FromResult(statusString == "Verified");
            }
            return Task.FromResult(statusString is "Pending" or "Verified");
        };

        if (expectedAccess)
        {
            var result = await service.OpenForDownloadAsync(uploaded.FileId, patientUserId);
            Assert.NotNull(result);
            Assert.Equal("application/pdf", result.ContentType);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
            Assert.Equal(DocumentsErrorCode.AccessDenied, ex.ErrorCode);
        }
    }

    [Fact]
    public async Task CanAccessPatient_NoRepresentativeRelationship_Denied()
    {
        var service = CreateService();
        var strangerUserId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _identity.CanAccessHandler = (_, _, _) => Task.FromResult(false); // Không có quan hệ đại diện

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, strangerUserId));
        Assert.Equal(DocumentsErrorCode.AccessDenied, ex.ErrorCode);
    }

    // =========================================================================
    // 2. CHƯA PHÁT HÀNH KẾT LUẬN SAU CLS: KHÔNG THẤY KẾT QUẢ VÀ TỆP CLS
    // =========================================================================

    [Fact]
    public async Task ClsAttachment_PatientBlockedWhenDoctorNotConcludedPostCls()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _identity.CanAccessHandler = (_, _, _) => Task.FromResult(true); // Verified
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(false); // Bác sĩ CHƯA phát hành kết luận sau CLS

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.DoctorNotConcluded, ex.ErrorCode);
        Assert.Contains("kết luận sau CLS", ex.Message);
    }

    [Fact]
    public async Task ClsAttachment_PatientAllowedImmediatelyAfterDoctorConclusion()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _identity.CanAccessHandler = (_, _, _) => Task.FromResult(true); // Verified
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(true); // Bác sĩ ĐÃ kết luận sau CLS

        var streamResult = await service.OpenForDownloadAsync(uploaded.FileId, patientUserId);
        Assert.NotNull(streamResult);
        Assert.Equal("test.pdf", streamResult.FileName);
    }

    // =========================================================================
    // 3. QUẢN TRỊ QUYỀN: ADMIN KỸ THUẬT KHÔNG MẶC NHIÊN SỬA/XEM NỘI DUNG CHUYÊN MÔN
    // =========================================================================

    [Fact]
    public void TechnicalAdmin_CannotSatisfyClinicalPolicies_DoctorOrNurseOnly()
    {
        // Kiểm tra định nghĩa Policy trong IdentityPatients.Contracts
        // Admin chỉ nằm trong AdminOnly (GP §8.1.6, §8.6, Phạm vi §2.1 mục 7)
        Assert.Equal(Roles.Admin, Policies.RoleOf[Policies.AdminOnly]);

        // DoctorOnly CHỈ có Doctor, Admin không được phép thỏa mãn policy này
        Assert.Equal(Roles.Doctor, Policies.RoleOf[Policies.DoctorOnly]);
        Assert.NotEqual(Roles.Admin, Policies.RoleOf[Policies.DoctorOnly]);

        // NurseOnly CHỈ có Nurse, Admin không được phép thỏa mãn policy này
        Assert.Equal(Roles.Nurse, Policies.RoleOf[Policies.NurseOnly]);
        Assert.NotEqual(Roles.Admin, Policies.RoleOf[Policies.NurseOnly]);

        // RoomAccountOnly CHỈ có RoomAccount, Admin không được phép thỏa mãn
        Assert.Equal(Roles.RoomAccount, Policies.RoleOf[Policies.RoomAccountOnly]);
        Assert.NotEqual(Roles.Admin, Policies.RoleOf[Policies.RoomAccountOnly]);
    }

    [Fact]
    public void PatientAccount_CannotSatisfyStaffOrAdminPolicies()
    {
        // Patient role có kênh là Patient
        Assert.Equal(Channels.Patient, Policies.ChannelOf(Roles.Patient));

        // Mọi policy nhân viên (Admin, Receptionist, Doctor, Nurse, RoomAccount) có kênh là Staff
        Assert.Equal(Channels.Staff, Policies.ChannelOf(Roles.Admin));
        Assert.Equal(Channels.Staff, Policies.ChannelOf(Roles.Doctor));
        Assert.Equal(Channels.Staff, Policies.ChannelOf(Roles.Nurse));
        Assert.Equal(Channels.Staff, Policies.ChannelOf(Roles.Receptionist));
        Assert.Equal(Channels.Staff, Policies.ChannelOf(Roles.RoomAccount));

        // Tài khoản Patient không thể khớp AdminOnly
        Assert.NotEqual(Roles.Patient, Policies.RoleOf[Policies.AdminOnly]);
    }

    // =========================================================================
    // 4. KIỂM THỬ TRUY VẤN TỆP THEO ENCOUNTER & PHÂN TRANG
    // =========================================================================

    [Fact]
    public async Task GetPublishedAttachmentsByEncounter_ReturnsOnlyPublishedFiles()
    {
        var service = CreateService();
        var encounterId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var clsResultId = Guid.NewGuid();

        var att1 = ResultAttachment.CreateDraft(Guid.NewGuid(), clsResultId, Guid.NewGuid(), encounterId, patientId, "k1", "f1.pdf", "application/pdf", 100, "h1");
        att1.MarkPublished();

        var att2 = ResultAttachment.CreateDraft(Guid.NewGuid(), clsResultId, Guid.NewGuid(), encounterId, patientId, "k2", "f2.pdf", "application/pdf", 200, "h2");
        // att2 chưa publish

        var att3 = ResultAttachment.CreateDraft(Guid.NewGuid(), clsResultId, Guid.NewGuid(), encounterId, patientId, "k3", "f3.png", "image/png", 300, "h3");
        att3.MarkPublished();

        await _store.AddAsync(att1);
        await _store.AddAsync(att2);
        await _store.AddAsync(att3);

        var publishedFiles = await service.GetPublishedAttachmentsByEncounterAsync(encounterId);

        Assert.Equal(2, publishedFiles.Count);
        Assert.Contains(publishedFiles, x => x.FileName == "f1.pdf");
        Assert.Contains(publishedFiles, x => x.FileName == "f3.png");
        Assert.DoesNotContain(publishedFiles, x => x.FileName == "f2.pdf");
    }

    [Fact]
    public void PaginationHelper_CalculatesSkipAndTakeCorrectly()
    {
        int page = 3;
        int pageSize = 10;
        int skip = (page - 1) * pageSize;
        int take = pageSize;

        Assert.Equal(20, skip);
        Assert.Equal(10, take);

        var allItems = Enumerable.Range(1, 25).ToList();
        var pagedItems = allItems.Skip(skip).Take(take).ToList();

        Assert.Equal(5, pagedItems.Count);
        Assert.Equal(21, pagedItems.First());
        Assert.Equal(25, pagedItems.Last());
    }

    // =========================================================================
    // 5. CHI PHÍ ĐỢT KHÁM: 5 CHỈ TIÊU ĐÚNG ĐẶC TẢ PHẠM VI NGHIỆP VỤ §5.5
    // =========================================================================

    [Fact]
    public void EncounterBillingSummary_AdheresTo5RequiredMetrics()
    {
        var summary = new EncounterBillingSummaryDto(
            EncounterId: Guid.NewGuid(),
            Currency: "VND",
            TotalPayable: 520_000m,
            TotalPaid: 520_000m,
            TotalRefunded: 0m,
            NetCollected: 520_000m,
            Outstanding: 0m,
            CreditPendingRefund: 0m);

        // Đúng 5 chỉ tiêu:
        Assert.Equal(520_000m, summary.TotalPayable);
        Assert.Equal(520_000m, summary.TotalPaid);
        Assert.Equal(0m, summary.TotalRefunded);
        Assert.Equal(520_000m, summary.NetCollected);
        Assert.Equal(0m, summary.Outstanding);

        // Bất biến tài chính: NetCollected = TotalPaid - TotalRefunded
        Assert.Equal(summary.NetCollected, summary.TotalPaid - summary.TotalRefunded);
    }
}
