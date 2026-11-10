using IdentityPatients.Application.Verifications;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using SqlServerTests;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class VerificationTests
{
    [Fact]
    public async Task Verify_FromPending_SucceedsAndWritesAuditLog()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var patient = new Patient
        {
            Id = patientId,
            FullName = "Nguyễn Văn Test",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male,
            NationalId = $"{Random.Shared.NextInt64(10_000_000, 99_999_999)}3456",
            PhoneNumber = "0900000001",
            Address = "123 Đường Test",
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Patients.Add(patient);

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = patientId,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IVerificationService>();
        var result = await service.VerifyAsync(staffUserId, rep.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal("Verified", result.Value.VerificationStatus);
        Assert.Equal("********3456", result.Value.MaskedNationalId);

        var auditLog = await sharedDbContext.Set<AuditLog>().FirstOrDefaultAsync(a => a.RecordId == rep.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(auditLog);
        Assert.Equal(staffUserId, auditLog.PerformedByUserId);
        Assert.Equal("identity.PatientRepresentative", auditLog.TableName);
        Assert.Equal(rep.Id, auditLog.RecordId);
        Assert.Contains("VerificationStatus", auditLog.ChangedColumns);
        Assert.Contains("Verified", auditLog.NewValuesJson);
    }

    [Fact]
    public async Task Reject_FromPending_WithReason_SucceedsAndRevokesActive()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var patientId = dbContext.AddPatient().Id;

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = patientId,
            Relationship = Relationship.Child,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IVerificationService>();
        var result = await service.RejectAsync(staffUserId, rep.Id, new RejectVerificationRequest("Giấy tờ khai sinh không hợp lệ"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal("Rejected", result.Value.VerificationStatus);

        var updatedRep = await dbContext.PatientRepresentatives.FindAsync([rep.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updatedRep);
        Assert.False(updatedRep.IsActive);
        Assert.NotNull(updatedRep.RevokedAtUtc);

        var auditLog = await sharedDbContext.Set<AuditLog>().FirstOrDefaultAsync(a => a.RecordId == rep.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(auditLog);
        Assert.Equal("Giấy tờ khai sinh không hợp lệ", auditLog.ChangeReason);
    }

    [Fact]
    public async Task Revoke_FromVerified_Succeeds()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var patientId = dbContext.AddPatient().Id;

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = patientId,
            Relationship = Relationship.Spouse,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IVerificationService>();
        var result = await service.RevokeAsync(staffUserId, rep.Id, new RevokeVerificationRequest("Thu hồi theo yêu cầu của bệnh nhân"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Revoked", result.Value?.VerificationStatus);

        var updatedRep = await dbContext.PatientRepresentatives.FindAsync([rep.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updatedRep);
        Assert.False(updatedRep.IsActive);
        Assert.NotNull(updatedRep.RevokedAtUtc);
    }

    [Fact]
    public async Task InvalidTransition_FromVerifiedToVerify_Returns409ConflictWithAllowedTransitions()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = dbContext.AddPatient().Id,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IVerificationService>();
        var result = await service.VerifyAsync(staffUserId, rep.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Chuyển trạng thái không hợp lệ", result.ErrorTitle);
        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("allowedTransitions"));
    }

    [Fact]
    public async Task RejectOrRevoke_WithoutReason_Returns400BadRequest()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = dbContext.AddPatient().Id,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IVerificationService>();
        var resultReject = await service.RejectAsync(staffUserId, rep.Id, new RejectVerificationRequest("   "), TestContext.Current.CancellationToken);
        Assert.False(resultReject.IsSuccess);
        Assert.Equal(400, resultReject.StatusCode);

        rep.VerificationStatus = VerificationStatus.Verified;
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resultRevoke = await service.RevokeAsync(staffUserId, rep.Id, new RevokeVerificationRequest(""), TestContext.Current.CancellationToken);
        Assert.False(resultRevoke.IsSuccess);
        Assert.Equal(400, resultRevoke.StatusCode);
    }
}
