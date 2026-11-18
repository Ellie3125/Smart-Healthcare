using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Xunit;

namespace SqlServerTests;

public sealed class IdentityPatientAudienceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Audience_matches_booking_access_and_excludes_inactive_rejected_revoked_staff_and_room_accounts()
    {
        await using var db = await TestDatabase.OpenAsync();
        var patient = await db.InsertAsync("identity.Patient");
        var otherPatient = await db.InsertAsync("identity.Patient");
        var expected = new List<Guid>(); var users = new List<Guid>();
        foreach (var state in new[] { "Pending", "Verified", "Rejected", "Revoked", "InactiveLink", "InactiveUser", "Staff", "Room", "OtherPatient" })
        {
            var user = await db.InsertAsync("identity.AppUser", new { IsActive = state != "InactiveUser" }); users.Add(user);
            await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = user,
                PatientId = state == "OtherPatient" ? otherPatient : patient, Relationship = "Self",
                VerificationStatus = state is "Pending" or "Rejected" or "Revoked" ? state : "Verified", IsActive = state != "InactiveLink" });
            if (state == "Staff") await db.InsertAsync("identity.Staff", new { AppUserId = user, StaffType = "Nurse", IsActive = false });
            if (state == "Room")
            {
                var room = await db.InsertAsync("identity.Room", new { RoomType = "CLS" });
                await db.InsertAsync("identity.RoomAccount", new { AppUserId = user, RoomId = room, IsActive = false });
            }
            if (state is "Pending" or "Verified") expected.Add(user);
        }
        await using var identityDb = db.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule identity = new IdentityPatientsModule(identityDb);
        var audience = await identity.GetPatientAppUserIdsAsync(patient, Ct);
        Assert.Equal(expected.Order(), audience.Order()); Assert.DoesNotContain(patient, audience);
        Assert.Equal(audience.Count, audience.Distinct().Count());
        foreach (var user in users)
            Assert.Equal(audience.Contains(user), await identity.CanAccessPatientAsync(user, patient, AccessLevel.Booking, Ct));
        Assert.False(await identity.CanAccessPatientAsync(expected[0], patient, AccessLevel.ClinicalRecords, Ct));
    }

    [Fact]
    public async Task Missing_patient_or_no_active_account_returns_empty_and_empty_id_is_rejected()
    {
        await using var db = await TestDatabase.OpenAsync();
        var patient = await db.InsertAsync("identity.Patient");
        await using var identityDb = db.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule identity = new IdentityPatientsModule(identityDb);
        Assert.Empty(await identity.GetPatientAppUserIdsAsync(Guid.NewGuid(), Ct));
        Assert.Empty(await identity.GetPatientAppUserIdsAsync(patient, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => identity.GetPatientAppUserIdsAsync(Guid.Empty, Ct));
    }
}
