using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class DoctorShiftRoomTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Returns_distinct_rooms_of_the_doctors_shifts_on_that_date()
    {
        await using var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var date = new DateOnly(2099, 3, 3);
        var user = context.AddUser(Roles.Doctor);
        var staff = context.Staffs.Local.Single(s => s.AppUserId == user.Id);
        var specialty = new Specialty { Id = Guid.NewGuid(), SpecialtyCode = IdentityTestData.Unique("SPEC"), SpecialtyName = "Khoa test", IsActive = true };
        var rank = await context.DoctorRanks.FirstOrDefaultAsync(r => r.RankCode == RankCode.Master, Ct)
            ?? new DoctorRank { Id = Guid.NewGuid(), RankCode = RankCode.Master, RankName = "Thạc sĩ", DisplayOrder = 1, IsActive = true };
        if (context.Entry(rank).State == EntityState.Detached)
            context.DoctorRanks.Add(rank);
        var doctor = new Doctor { Id = Guid.NewGuid(), StaffId = staff.Id, SpecialtyId = specialty.Id, DoctorRankId = rank.Id, IsActive = true };
        context.AddRange(specialty, doctor);
        var morningRoom = context.AddRoom();
        var otherDayRoom = context.AddRoom();
        Shift(context, staff.Id, morningRoom.Id, date, ShiftName.Morning);
        Shift(context, staff.Id, morningRoom.Id, date, ShiftName.Afternoon);
        Shift(context, staff.Id, otherDayRoom.Id, date.AddDays(1), ShiftName.Morning);
        await context.SaveChangesAsync(Ct);
        var module = new IdentityPatientsModule(context);

        Assert.Equal([morningRoom.Id], await module.GetDoctorShiftRoomIdsAsync(doctor.Id, date, Ct));
        Assert.Empty(await module.GetDoctorShiftRoomIdsAsync(doctor.Id, date.AddDays(2), Ct));
        Assert.Empty(await module.GetDoctorShiftRoomIdsAsync(Guid.NewGuid(), date, Ct));
    }

    private static void Shift(IdentityPatients.Infrastructure.Persistence.IdentityPatientsDbContext context, Guid staffId, Guid roomId,
        DateOnly date, ShiftName name) =>
        context.StaffShiftAssignments.Add(new StaffShiftAssignment
        {
            Id = Guid.NewGuid(), StaffId = staffId, RoomId = roomId, ShiftDate = date, ShiftName = name,
            StartsAtUtc = date.ToDateTime(new TimeOnly(1, 0), DateTimeKind.Utc), EndsAtUtc = date.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc),
        });
}
