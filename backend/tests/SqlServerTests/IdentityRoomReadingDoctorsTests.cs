using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using IdentityPatients.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SqlServerTests;

public sealed class IdentityRoomReadingDoctorsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime Now = new(2097, 8, 5, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reader_returns_real_staff_and_doctor_ids_only_for_active_doctors_currently_on_duty_in_this_room()
    {
        await using var sql = await TestDatabase.OpenAsync();
        var room = await sql.InsertAsync("identity.Room", new { RoomType = "Laboratory", IsActive = true });
        var other = await sql.InsertAsync("identity.Room", new { RoomType = "Laboratory", IsActive = true });
        var specialty = await sql.InsertAsync("identity.Specialty", new { IsActive = true });
        var rank = await sql.InsertAsync("identity.DoctorRank", new { RankCode = "SpecialistI", IsActive = true });
        var expected = new List<RoomReadingDoctorDto>();
        foreach (var state in new[] { "Active", "AtStart", "AtEnd", "Future", "OtherRoom", "WrongDate", "InactiveDoctor", "InactiveStaff", "InactiveUser", "Nurse" })
        {
            var user = await sql.InsertAsync("identity.AppUser", new { IsActive = state != "InactiveUser" });
            var staff = await sql.InsertAsync("identity.Staff", new { AppUserId = user, FullName = state,
                StaffType = state == "Nurse" ? "Nurse" : "Doctor", IsActive = state != "InactiveStaff" });
            var doctor = await sql.InsertAsync("identity.Doctor", new { StaffId = staff, SpecialtyId = specialty, DoctorRankId = rank, IsActive = state != "InactiveDoctor" });
            var day = Now.AddHours(7).Date;
            var assignment = new { StaffId = staff, RoomId = state == "OtherRoom" ? other : room,
                ShiftDate = state == "WrongDate" ? day.AddDays(-1) : day, ShiftName = "Morning",
                StartsAtUtc = state == "AtStart" ? Now : state == "Future" ? Now.AddMinutes(1) : Now.AddHours(-1),
                EndsAtUtc = state == "AtEnd" ? Now : Now.AddHours(1) };
            await sql.InsertAsync("identity.StaffShiftAssignment", assignment);
            if (state == "Active") await sql.InsertAsync("identity.StaffShiftAssignment", assignment);
            if (state is "Active" or "AtStart") expected.Add(new(doctor, staff, state));
        }
        await using var db = sql.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule module = new IdentityPatientsModule(db);
        var found = await module.GetRoomReadingDoctorsAsync(room, Now, Ct);
        Assert.Equal(expected.OrderBy(x => x.DoctorId), found.OrderBy(x => x.DoctorId));
        Assert.All(found, d => Assert.NotEqual(d.DoctorId, d.StaffId));
        await db.Rooms.Where(x => x.Id == room).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        Assert.Empty(await module.GetRoomReadingDoctorsAsync(room, Now, Ct));
        await db.Rooms.Where(x => x.Id == room).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true).SetProperty(x => x.RoomType, RoomType.Clinical), Ct);
        Assert.Empty(await module.GetRoomReadingDoctorsAsync(room, Now, Ct));
    }

    [Fact]
    public async Task Missing_room_and_empty_room_return_no_doctors_and_invalid_arguments_are_rejected()
    {
        await using var sql = await TestDatabase.OpenAsync();
        var room = await sql.InsertAsync("identity.Room", new { RoomType = "Laboratory", IsActive = true });
        await using var db = sql.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule module = new IdentityPatientsModule(db);
        Assert.Empty(await module.GetRoomReadingDoctorsAsync(room, Now, Ct));
        Assert.Empty(await module.GetRoomReadingDoctorsAsync(Guid.NewGuid(), Now, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => module.GetRoomReadingDoctorsAsync(Guid.Empty, Now, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => module.GetRoomReadingDoctorsAsync(room, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), Ct));
    }
}
