using IdentityPatients.Application.Services;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace IdentityPatients.Tests;

/// <summary>
/// Unit tests cho CanAccessPatientAsync — kiểm tra logic quyền truy cập hồ sơ bệnh nhân.
/// Dùng EF InMemory provider vì đây là unit test logic, không phải integration test DB.
/// (DB-06: test SQL Server thật áp dụng cho integration tests trong SqlServerTests project.)
/// </summary>
public sealed class CanAccessPatientTests : IDisposable
{
    private readonly IdentityPatientsDbContext _db;
    private readonly IdentityPatientsModule _sut;

    // Dữ liệu cố định cho test
    private readonly Guid _patientProfileId = Guid.NewGuid();
    private readonly Guid _otherPatientProfileId = Guid.NewGuid();

    private readonly Guid _doctorUserId = Guid.NewGuid();
    private readonly Guid _nurseUserId = Guid.NewGuid();
    private readonly Guid _receptionistUserId = Guid.NewGuid();
    private readonly Guid _patientUserId = Guid.NewGuid();
    private readonly Guid _roomAccountUserId = Guid.NewGuid();
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _inactiveUserId = Guid.NewGuid();
    private readonly Guid _nonExistentUserId = Guid.NewGuid();

    public CanAccessPatientTests()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // DB riêng mỗi test class
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new IdentityPatientsDbContext(options);
        _sut = new IdentityPatientsModule(_db);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Nhân viên y tế
        _db.AppUsers.AddRange(
            MakeUser(_doctorUserId, "bstest", Roles.Doctor, isActive: true),
            MakeUser(_nurseUserId, "ddtest", Roles.Nurse, isActive: true),
            MakeUser(_receptionistUserId, "lttest", Roles.Receptionist, isActive: true),
            MakeUser(_patientUserId, "bntest", Roles.Patient, isActive: true),
            MakeUser(_roomAccountUserId, "xqtest", Roles.RoomAccount, isActive: true),
            MakeUser(_adminUserId, "admintest", Roles.Admin, isActive: true),
            MakeUser(_inactiveUserId, "inactive", Roles.Doctor, isActive: false)
        );

        // Hồ sơ bệnh nhân: patient01 thuộc patientUser
        _db.Patients.AddRange(
            new Patient { Id = _patientProfileId, OwnerUserId = _patientUserId },
            new Patient { Id = _otherPatientProfileId, OwnerUserId = Guid.NewGuid() } // chủ sở hữu khác
        );

        _db.SaveChanges();
    }

    private static AppUser MakeUser(Guid id, string userName, string role, bool isActive) =>
        new()
        {
            Id = id,
            UserName = userName,
            PasswordHash = "not-used-in-test",
            Role = role,
            IsActive = isActive,
            RowVersion = new byte[8]
        };

    // ─── Nhân viên y tế được phép ───────────────────────────────────────────

    [Fact]
    public async Task Doctor_can_access_any_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_doctorUserId, _patientProfileId);
        Assert.True(result);
    }

    [Fact]
    public async Task Nurse_can_access_any_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_nurseUserId, _patientProfileId);
        Assert.True(result);
    }

    [Fact]
    public async Task Receptionist_can_access_any_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_receptionistUserId, _patientProfileId);
        Assert.True(result);
    }

    // ─── Bệnh nhân: chỉ được xem hồ sơ của chính mình ──────────────────────

    [Fact]
    public async Task Patient_can_access_own_profile()
    {
        var result = await _sut.CanAccessPatientAsync(_patientUserId, _patientProfileId);
        Assert.True(result);
    }

    [Fact]
    public async Task Patient_cannot_access_other_patient_profile()
    {
        var result = await _sut.CanAccessPatientAsync(_patientUserId, _otherPatientProfileId);
        Assert.False(result);
    }

    // ─── Vai trò bị từ chối ─────────────────────────────────────────────────

    [Fact]
    public async Task RoomAccount_cannot_access_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_roomAccountUserId, _patientProfileId);
        Assert.False(result);
    }

    [Fact]
    public async Task Admin_cannot_access_patient_records()
    {
        // Admin kỹ thuật không mặc nhiên đọc nội dung chuyên môn (SEC-01)
        var result = await _sut.CanAccessPatientAsync(_adminUserId, _patientProfileId);
        Assert.False(result);
    }

    // ─── User không tồn tại hoặc không active ───────────────────────────────

    [Fact]
    public async Task Nonexistent_user_cannot_access_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_nonExistentUserId, _patientProfileId);
        Assert.False(result);
    }

    [Fact]
    public async Task Inactive_user_cannot_access_patient()
    {
        var result = await _sut.CanAccessPatientAsync(_inactiveUserId, _patientProfileId);
        Assert.False(result);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

