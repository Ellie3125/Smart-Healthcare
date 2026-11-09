using IdentityPatients.Application.Verifications;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class VerificationTests
{
    private static (IdentityPatientsDbContext dbContext, SharedDbContext sharedDbContext) CreateInMemoryContexts()
    {
        var dbName = Guid.NewGuid().ToString();

        var idOptions = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseInMemoryDatabase($"Identity_{dbName}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var sharedOptions = new DbContextOptionsBuilder<SharedDbContext>()
            .UseInMemoryDatabase($"Shared_{dbName}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return (new IdentityPatientsDbContext(idOptions), new SharedDbContext(sharedOptions));
    }

    [Fact]
    public async Task Verify_FromPending_SucceedsAndWritesAuditLog()
    {
        var (dbContext, sharedDbContext) = CreateInMemoryContexts();
        var staffUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var patient = new Patient
        {
            Id = patientId,
            FullName = "Nguyễn Văn Test",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male,
            NationalId = "001090123456",
            PhoneNumber = "0900000001",
            Address = "123 Đường Test",
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Patients.Add(patient);

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = patientId,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync();

        var service = new VerificationService(dbContext, sharedDbContext);
        var result = await service.VerifyAsync(staffUserId, rep.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal("Verified", result.Value.VerificationStatus);
        Assert.Equal("********3456", result.Value.MaskedNationalId);

        var auditLog = await sharedDbContext.Set<AuditLog>().FirstOrDefaultAsync();
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
        var (dbContext, sharedDbContext) = CreateInMemoryContexts();
        var staffUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = patientId,
            Relationship = Relationship.Child,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync();

        var service = new VerificationService(dbContext, sharedDbContext);
        var result = await service.RejectAsync(staffUserId, rep.Id, new RejectVerificationRequest("Giấy tờ khai sinh không hợp lệ"));

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
        Assert.Equal("Rejected", result.Value.VerificationStatus);

        var updatedRep = await dbContext.PatientRepresentatives.FindAsync(rep.Id);
        Assert.NotNull(updatedRep);
        Assert.False(updatedRep.IsActive);
        Assert.NotNull(updatedRep.RevokedAtUtc);

        var auditLog = await sharedDbContext.Set<AuditLog>().FirstOrDefaultAsync();
        Assert.NotNull(auditLog);
        Assert.Equal("Giấy tờ khai sinh không hợp lệ", auditLog.ChangeReason);
    }

    [Fact]
    public async Task Revoke_FromVerified_Succeeds()
    {
        var (dbContext, sharedDbContext) = CreateInMemoryContexts();
        var staffUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = patientId,
            Relationship = Relationship.Spouse,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync();

        var service = new VerificationService(dbContext, sharedDbContext);
        var result = await service.RevokeAsync(staffUserId, rep.Id, new RevokeVerificationRequest("Thu hồi theo yêu cầu của bệnh nhân"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Revoked", result.Value?.VerificationStatus);

        var updatedRep = await dbContext.PatientRepresentatives.FindAsync(rep.Id);
        Assert.NotNull(updatedRep);
        Assert.False(updatedRep.IsActive);
        Assert.NotNull(updatedRep.RevokedAtUtc);
    }

    [Fact]
    public async Task InvalidTransition_FromVerifiedToVerify_Returns409ConflictWithAllowedTransitions()
    {
        var (dbContext, sharedDbContext) = CreateInMemoryContexts();
        var staffUserId = Guid.NewGuid();
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync();

        var service = new VerificationService(dbContext, sharedDbContext);
        var result = await service.VerifyAsync(staffUserId, rep.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Chuyển trạng thái không hợp lệ", result.ErrorTitle);
        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("allowedTransitions"));
    }

    [Fact]
    public async Task RejectOrRevoke_WithoutReason_Returns400BadRequest()
    {
        var (dbContext, sharedDbContext) = CreateInMemoryContexts();
        var staffUserId = Guid.NewGuid();
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync();

        var service = new VerificationService(dbContext, sharedDbContext);
        var resultReject = await service.RejectAsync(staffUserId, rep.Id, new RejectVerificationRequest("   "));
        Assert.False(resultReject.IsSuccess);
        Assert.Equal(400, resultReject.StatusCode);

        rep.VerificationStatus = VerificationStatus.Verified;
        await dbContext.SaveChangesAsync();

        var resultRevoke = await service.RevokeAsync(staffUserId, rep.Id, new RevokeVerificationRequest(""));
        Assert.False(resultRevoke.IsSuccess);
        Assert.Equal(400, resultRevoke.StatusCode);
    }
}
