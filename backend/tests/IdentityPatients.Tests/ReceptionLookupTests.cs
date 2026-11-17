using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Services;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class ReceptionLookupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Finds_patients_by_exact_phone_or_national_id_with_masked_identifiers()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var phone = "09" + Random.Shared.NextInt64(10_000_000, 99_999_999);
        var nationalId = "0" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var verified = context.AddPatient(nationalId);
        verified.PhoneNumber = phone;
        var sibling = context.AddPatient();
        sibling.PhoneNumber = phone;
        var representative = context.AddUser(Roles.Patient);
        context.PatientRepresentatives.Add(new PatientRepresentative
        {
            Id = Guid.NewGuid(), AppUserId = representative.Id, PatientId = verified.Id, Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Verified, IsActive = true, CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync(Ct);
        var module = new IdentityPatientsModule(context);

        var byPhone = await module.FindPatientsForReceptionAsync($" {phone[..4]} {phone[4..]} ", Ct);
        Assert.Equal(2, byPhone.Count);
        var found = Assert.Single(byPhone, x => x.PatientId == verified.Id);
        Assert.True(found.IsVerified);
        Assert.False(Assert.Single(byPhone, x => x.PatientId == sibling.Id).IsVerified);
        Assert.Equal(new string('*', phone.Length - 3) + phone[^3..], found.MaskedPhoneNumber);
        Assert.Equal(new string('*', 9) + nationalId[^3..], found.MaskedNationalId);

        Assert.Equal(verified.Id, Assert.Single(await module.FindPatientsForReceptionAsync(nationalId, Ct)).PatientId);
        Assert.Empty(await module.FindPatientsForReceptionAsync(phone[..^1], Ct)); // không tìm gần đúng
        Assert.Empty(await module.FindPatientsForReceptionAsync("12", Ct));
        Assert.True(await module.PatientExistsAsync(sibling.Id, Ct));
        Assert.False(await module.PatientExistsAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Active_staff_is_resolved_from_app_user()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var nurse = context.AddUser(Roles.Nurse);
        var lockedNurse = context.AddUser(Roles.Nurse, isActive: false);
        var patient = context.AddUser(Roles.Patient);
        await context.SaveChangesAsync(Ct);
        var module = new IdentityPatientsModule(context);

        var staff = await module.GetActiveStaffAsync(nurse.Id, Ct);
        Assert.Equal(context.Staffs.Single(s => s.AppUserId == nurse.Id).Id, staff?.StaffId);
        Assert.Equal(nameof(StaffType.Nurse), staff?.StaffType);
        Assert.Null(await module.GetActiveStaffAsync(lockedNurse.Id, Ct));
        Assert.Null(await module.GetActiveStaffAsync(patient.Id, Ct));
    }
}
