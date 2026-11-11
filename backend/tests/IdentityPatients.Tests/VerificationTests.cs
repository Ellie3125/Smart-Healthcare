using IdentityPatients.Application.Patients;
using IdentityPatients.Application.Verifications;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Domain.StateMachines;
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
    public async Task InvalidTransitions_Return409ConflictWithAllowedTransitions()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
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

        // Verified -> Rejected bị cấm
        var rejectVerified = await service.RejectAsync(
            staffUserId,
            rep.Id,
            new RejectVerificationRequest("Sai hồ sơ"),
            TestContext.Current.CancellationToken);
        Assert.False(rejectVerified.IsSuccess);
        Assert.Equal(409, rejectVerified.StatusCode);
        Assert.Equal(VerificationStateMachine.InvalidTransitionCode, rejectVerified.ErrorCode);
        Assert.NotNull(rejectVerified.Extensions);
        Assert.True(rejectVerified.Extensions.ContainsKey("allowedTransitions"));

        // Thu hồi -> Verified bị cấm (trạng thái cuối)
        var revoke = await service.RevokeAsync(
            staffUserId,
            rep.Id,
            new RevokeVerificationRequest("Thu hồi test"),
            TestContext.Current.CancellationToken);
        Assert.True(revoke.IsSuccess);

        var verifyRevoked = await service.VerifyAsync(staffUserId, rep.Id, TestContext.Current.CancellationToken);
        Assert.False(verifyRevoked.IsSuccess);
        Assert.Equal(409, verifyRevoked.StatusCode);
        Assert.Equal(VerificationStateMachine.InvalidTransitionCode, verifyRevoked.ErrorCode);
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

    [Fact]
    public async Task RepeatedTransitions_AreIdempotent_AndDoNotCreateExtraAuditLogs()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var staffUserId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var repVerify = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = dbContext.AddPatient().Id,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var repReject = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            PatientId = dbContext.AddPatient().Id,
            Relationship = Relationship.Child,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.AddRange(repVerify, repReject);
        await dbContext.SaveChangesAsync(ct);

        var service = host.Get<IVerificationService>();

        var firstVerify = await service.VerifyAsync(staffUserId, repVerify.Id, ct);
        var secondVerify = await service.VerifyAsync(staffUserId, repVerify.Id, ct);
        Assert.True(firstVerify.IsSuccess);
        Assert.True(secondVerify.IsSuccess);
        Assert.Equal("Verified", secondVerify.Value!.VerificationStatus);

        var firstReject = await service.RejectAsync(staffUserId, repReject.Id, new RejectVerificationRequest("Ảnh mờ"), ct);
        var secondReject = await service.RejectAsync(staffUserId, repReject.Id, new RejectVerificationRequest("Gọi lại lần 2"), ct);
        Assert.True(firstReject.IsSuccess);
        Assert.True(secondReject.IsSuccess);
        Assert.Equal("Rejected", secondReject.Value!.VerificationStatus);

        var firstRevoke = await service.RevokeAsync(staffUserId, repVerify.Id, new RevokeVerificationRequest("Thu hồi"), ct);
        var secondRevoke = await service.RevokeAsync(staffUserId, repVerify.Id, new RevokeVerificationRequest("Thu hồi lặp"), ct);
        Assert.True(firstRevoke.IsSuccess);
        Assert.True(secondRevoke.IsSuccess);
        Assert.Equal("Revoked", secondRevoke.Value!.VerificationStatus);

        Assert.Equal(2, await sharedDbContext.Set<AuditLog>().CountAsync(a => a.RecordId == repVerify.Id, ct));
        Assert.Equal(1, await sharedDbContext.Set<AuditLog>().CountAsync(a => a.RecordId == repReject.Id, ct));
    }

    [Fact]
    public async Task Resubmit_FromRejected_TransitionsBackToPending_UpdatesSameRowAndAudits()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var userId = dbContext.AddUser(Roles.Patient).Id;
        var patientId = dbContext.AddPatient().Id;
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patientId,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Rejected,
            IsActive = false,
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(ct);

        var profiles = host.Get<IPatientProfileService>();
        var resubmit1 = await profiles.ResubmitProfileAsync(userId, patientId, ct);
        Assert.True(resubmit1.IsSuccess);
        Assert.Equal("Pending", resubmit1.Value!.VerificationStatus);
        Assert.True(resubmit1.Value.IsActive);
        Assert.Equal(rep.Id, resubmit1.Value.RepresentativeId);

        // Idempotent lần 2 khi đã Pending
        var resubmit2 = await profiles.ResubmitProfileAsync(userId, patientId, ct);
        Assert.True(resubmit2.IsSuccess);
        Assert.Equal("Pending", resubmit2.Value!.VerificationStatus);

        dbContext.ChangeTracker.Clear();
        var updatedRep = await dbContext.PatientRepresentatives.SingleAsync(r => r.Id == rep.Id, ct);
        Assert.Equal(VerificationStatus.Pending, updatedRep.VerificationStatus);
        Assert.True(updatedRep.IsActive);
        Assert.Null(updatedRep.RevokedAtUtc);

        var logs = await sharedDbContext.Set<AuditLog>().Where(a => a.RecordId == rep.Id).ToListAsync(ct);
        Assert.Single(logs);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending, VerificationStatus.Verified, true, false, true)]
    [InlineData(VerificationStatus.Pending, VerificationStatus.Rejected, true, false, false)]
    [InlineData(VerificationStatus.Verified, VerificationStatus.Revoked, true, false, false)]
    [InlineData(VerificationStatus.Rejected, VerificationStatus.Pending, true, false, true)]
    [InlineData(VerificationStatus.Pending, VerificationStatus.Pending, true, true, true)]
    [InlineData(VerificationStatus.Verified, VerificationStatus.Verified, true, true, true)]
    [InlineData(VerificationStatus.Rejected, VerificationStatus.Rejected, true, true, false)]
    [InlineData(VerificationStatus.Revoked, VerificationStatus.Revoked, true, true, false)]
    [InlineData(VerificationStatus.Pending, VerificationStatus.Revoked, false, false, true)]
    [InlineData(VerificationStatus.Verified, VerificationStatus.Rejected, false, false, true)]
    [InlineData(VerificationStatus.Rejected, VerificationStatus.Verified, false, false, false)]
    [InlineData(VerificationStatus.Revoked, VerificationStatus.Pending, false, false, false)]
    [InlineData(VerificationStatus.Revoked, VerificationStatus.Verified, false, false, false)]
    public void VerificationStateMachine_UnitMatrix_EnforcesRulesAndIdempotency(
        VerificationStatus fromStatus,
        VerificationStatus toStatus,
        bool expectedSuccess,
        bool expectedIdempotent,
        bool initialActive)
    {
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Relationship = Relationship.Self,
            VerificationStatus = fromStatus,
            IsActive = initialActive,
            RevokedAtUtc = initialActive ? null : DateTime.UtcNow.AddHours(-1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var outcome = VerificationStateMachine.ApplyTransition(rep, toStatus, DateTime.UtcNow);
        Assert.Equal(expectedSuccess, outcome.Succeeded);
        Assert.Equal(expectedIdempotent, outcome.IsIdempotent);
        if (!expectedSuccess)
        {
            Assert.Equal(VerificationStateMachine.InvalidTransitionCode, outcome.ErrorCode);
            Assert.NotNull(outcome.AllowedTransitions);
        }
    }
}
