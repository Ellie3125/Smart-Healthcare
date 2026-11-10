using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class CanAccessPatientTests
{
    private static async Task SeedRoleAndUserAsync(IdentityPatientsDbContext context, Guid userId, string roleName, bool isActive = true)
    {
        context.AddUser(roleName, isActive).Id = userId;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanAccessPatient_WhenNoRepresentativeRecord_ReturnsFalse()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var userId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, TestContext.Current.CancellationToken);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsPending_AllowsBooking_DeniesClinicalRecords()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var userId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        context.PatientRepresentatives.Add(new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patientId,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, TestContext.Current.CancellationToken);

        Assert.True(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsVerified_AllowsBookingAndClinicalRecords()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var userId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        context.PatientRepresentatives.Add(new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patientId,
            Relationship = Relationship.Child,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, TestContext.Current.CancellationToken);

        Assert.True(canAccessBooking);
        Assert.True(canAccessClinical);
    }

    [Theory]
    [InlineData(VerificationStatus.Rejected)]
    [InlineData(VerificationStatus.Revoked)]
    public async Task CanAccessPatient_WhenStatusIsRejectedOrRevoked_DeniesAll(VerificationStatus status)
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var userId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        context.PatientRepresentatives.Add(new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patientId,
            Relationship = Relationship.Other,
            VerificationStatus = status,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, TestContext.Current.CancellationToken);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsInactive_ReturnsFalse()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var userId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        context.PatientRepresentatives.Add(new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patientId,
            Relationship = Relationship.Parent,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, TestContext.Current.CancellationToken);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenCalledByStaffOrRoomAccount_ReturnsFalse()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var staffUserId = Guid.NewGuid();
        var roomUserId = Guid.NewGuid();
        var patientId = context.AddPatient().Id;

        await SeedRoleAndUserAsync(context, staffUserId, Roles.Receptionist);
        await SeedRoleAndUserAsync(context, roomUserId, Roles.RoomAccount);

        var module = new IdentityPatientsModule(context);

        Assert.False(await module.CanAccessPatientAsync(staffUserId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken));
        Assert.False(await module.CanAccessPatientAsync(roomUserId, patientId, AccessLevel.Booking, TestContext.Current.CancellationToken));
    }
}
