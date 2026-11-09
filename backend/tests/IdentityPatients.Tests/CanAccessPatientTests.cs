using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class CanAccessPatientTests
{
    private static IdentityPatientsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new IdentityPatientsDbContext(options);
    }

    private static async Task SeedRoleAndUserAsync(IdentityPatientsDbContext context, Guid userId, string roleName, bool isActive = true)
    {
        var role = new ApplicationRole(roleName) { Id = Guid.NewGuid() };
        context.Roles.Add(role);

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId:N}",
            IsActive = isActive
        };
        context.Users.Add(user);

        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = role.Id
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CanAccessPatient_WhenNoRepresentativeRecord_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        await SeedRoleAndUserAsync(context, userId, Roles.Patient);

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsPending_AllowsBooking_DeniesClinicalRecords()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

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
        await context.SaveChangesAsync();

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords);

        Assert.True(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsVerified_AllowsBookingAndClinicalRecords()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

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
        await context.SaveChangesAsync();

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords);

        Assert.True(canAccessBooking);
        Assert.True(canAccessClinical);
    }

    [Theory]
    [InlineData(VerificationStatus.Rejected)]
    [InlineData(VerificationStatus.Revoked)]
    public async Task CanAccessPatient_WhenStatusIsRejectedOrRevoked_DeniesAll(VerificationStatus status)
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

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
        await context.SaveChangesAsync();

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenRepresentativeIsInactive_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

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
        await context.SaveChangesAsync();

        var module = new IdentityPatientsModule(context);
        var canAccessBooking = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking);
        var canAccessClinical = await module.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords);

        Assert.False(canAccessBooking);
        Assert.False(canAccessClinical);
    }

    [Fact]
    public async Task CanAccessPatient_WhenCalledByStaffOrRoomAccount_ReturnsFalse()
    {
        using var context = CreateInMemoryContext();
        var staffUserId = Guid.NewGuid();
        var roomUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        await SeedRoleAndUserAsync(context, staffUserId, Roles.Receptionist);
        await SeedRoleAndUserAsync(context, roomUserId, Roles.RoomAccount);

        var module = new IdentityPatientsModule(context);

        Assert.False(await module.CanAccessPatientAsync(staffUserId, patientId, AccessLevel.Booking));
        Assert.False(await module.CanAccessPatientAsync(roomUserId, patientId, AccessLevel.Booking));
    }
}
