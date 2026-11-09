using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.DoctorDays;
using Scheduling.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

public sealed class SchedulingDoctorDayTests
{
    private static readonly DateOnly ServiceDate = new(2026, 11, 10);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(11, 4)]
    [InlineData(12, 5)]
    public async Task PersistsQuotaAndConfiguredUtcDeadline(int deadlineIctHour, int deadlineUtcHour)
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var writer = database.CreateContext<SchedulingDbContext>(options => new(options));
        await using var reader = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var service = new DoctorDayService(new DoctorDirectory(roomId), new SqlDoctorDayStore(writer), new(new TimeOnly(deadlineIctHour, 0)));

        var (status, quota) = await service.OpenAsync(doctorId, ServiceDate, Ct);

        Assert.Equal(DoctorDayResult.Opened, status);
        Assert.NotNull(quota);
        var saved = await reader.Set<DoctorDayQuota>().SingleAsync(x => x.Id == quota.Id, Ct);
        Assert.Equal((doctorId, roomId, ServiceDate), (saved.DoctorId, saved.AssignedRoomId, saved.ServiceDate));
        Assert.Equal((30, 0, 0, 0), (saved.Capacity, saved.HeldCount, saved.AdmittedCount, saved.LockedCancelledCount));
        Assert.True(saved.IsOpen);
        Assert.Equal(new DateTime(2026, 11, 10, deadlineUtcHour, 0, 0, DateTimeKind.Utc), saved.CheckInDeadlineUtc);
        Assert.Equal(DateTimeKind.Utc, saved.CheckInDeadlineUtc!.Value.Kind);
    }

    [Fact]
    public async Task UniqueIndexRejectsDuplicateAcrossContexts()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var first = database.CreateContext<SchedulingDbContext>(options => new(options));
        await using var second = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var day = ServiceDate.ToDateTime(TimeOnly.MinValue);
        var initial = await database.InsertAsync("scheduling.DoctorDayQuota", new
        {
            DoctorId = doctorId, ServiceDate = day, Capacity = 30, IsOpen = true, SessionStatus = "Scheduled"
        });
        await database.RejectsAsync(TestDatabase.UniqueIndexViolation, "scheduling.DoctorDayQuota",
            new { DoctorId = doctorId, ServiceDate = day, Capacity = 30 });

        var (status, quota) = await Service(first, roomId).OpenAsync(doctorId, ServiceDate, Ct);

        Assert.Equal(DoctorDayResult.AlreadyExists, status);
        Assert.Null(quota);
        Assert.Equal(initial, await second.Set<DoctorDayQuota>()
            .Where(x => x.DoctorId == doctorId && x.ServiceDate == ServiceDate).Select(x => x.Id).SingleAsync(Ct));
        Assert.Equal(DoctorDayResult.Opened, (await Service(first, roomId).OpenAsync(doctorId, ServiceDate.AddDays(1), Ct)).Status);
    }

    [Fact]
    public async Task CloseRetainsQuotaAndIsIdempotent()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var db = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var service = Service(db, Guid.NewGuid());

        Assert.Equal(DoctorDayResult.DayNotFound, await service.CloseAsync(doctorId, ServiceDate, Ct));
        var (_, opened) = await service.OpenAsync(doctorId, ServiceDate, Ct);
        Assert.NotNull(opened);
        Assert.Equal(DoctorDayResult.Closed, await service.CloseAsync(doctorId, ServiceDate, Ct));
        Assert.Equal(DoctorDayResult.Closed, await service.CloseAsync(doctorId, ServiceDate, Ct));
        Assert.False(await db.Set<DoctorDayQuota>().Where(x => x.Id == opened.Id).Select(x => x.IsOpen).SingleAsync(Ct));
        Assert.Equal(1, await db.Set<DoctorDayQuota>().CountAsync(x => x.DoctorId == doctorId && x.ServiceDate == ServiceDate, Ct));
    }

    [Fact]
    public async Task ReopenReturnsTheExistingQuotaWithItsOriginalSettings()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var db = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var service = Service(db, roomId);
        var (_, opened) = await service.OpenAsync(doctorId, ServiceDate, Ct);
        Assert.NotNull(opened);
        Assert.Equal(DoctorDayResult.Closed, await service.CloseAsync(doctorId, ServiceDate, Ct));

        var newPolicy = new DoctorDayService(new DoctorDirectory(Guid.NewGuid()), new SqlDoctorDayStore(db), new(new TimeOnly(12, 0)));
        var (status, reopened) = await newPolicy.OpenAsync(doctorId, ServiceDate, Ct);

        Assert.Equal(DoctorDayResult.Opened, status);
        Assert.NotNull(reopened);
        Assert.Equal(opened.Id, reopened.Id);
        Assert.Equal(roomId, reopened.AssignedRoomId);
        Assert.Equal(opened.CheckInDeadlineUtc, reopened.CheckInDeadlineUtc);
        Assert.True(reopened.IsOpen);
        var saved = await db.Set<DoctorDayQuota>().AsNoTracking()
            .SingleAsync(x => x.DoctorId == doctorId && x.ServiceDate == ServiceDate, Ct);
        Assert.Equal(reopened.Id, saved.Id);
        Assert.True(saved.IsOpen);
        Assert.Equal(saved.RowVersion, reopened.RowVersion);
        Assert.Equal(DoctorDayResult.AlreadyExists, (await newPolicy.OpenAsync(doctorId, ServiceDate, Ct)).Status);
    }

    [Theory]
    [InlineData("Held", null, DoctorDayResult.HasBookings)]
    [InlineData("Confirmed", null, DoctorDayResult.HasBookings)]
    [InlineData("Expired", null, DoctorDayResult.Closed)]
    [InlineData("Cancelled", null, DoctorDayResult.Closed)]
    [InlineData(null, "Scheduled", DoctorDayResult.HasBookings)]
    [InlineData(null, "Cancelled", DoctorDayResult.Closed)]
    [InlineData(null, "Rescheduled", DoctorDayResult.Closed)]
    public async Task CloseUsesBookingStatusWhenCountersAreZero(
        string? reservationStatus, string? appointmentStatus, DoctorDayResult expected)
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var db = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var service = Service(db, Guid.NewGuid());
        var (_, opened) = await service.OpenAsync(doctorId, ServiceDate, Ct);
        Assert.NotNull(opened);

        var reservationQuotaId = appointmentStatus is not null
            ? await database.InsertAsync("scheduling.DoctorDayQuota", new { Capacity = 30 })
            : opened.Id;
        var reservationId = await database.InsertAsync("scheduling.Reservation", new
        {
            SessionQuotaId = reservationQuotaId,
            BookingAttemptId = await database.InsertAsync("scheduling.BookingAttempt"),
            Status = reservationStatus ?? "Confirmed"
        });
        if (appointmentStatus is not null)
        {
            await database.InsertAsync("scheduling.Appointment", new
            {
                SessionQuotaId = opened.Id,
                ReservationId = reservationId,
                Status = appointmentStatus
            });
        }

        Assert.Equal(expected, await service.CloseAsync(doctorId, ServiceDate, Ct));
        Assert.Equal(expected == DoctorDayResult.HasBookings,
            await db.Set<DoctorDayQuota>().Where(x => x.Id == opened.Id).Select(x => x.IsOpen).SingleAsync(Ct));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public async Task CloseRejectsOccupiedQuota(int heldCount, int admittedCount, int lockedCancelledCount)
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var db = database.CreateContext<SchedulingDbContext>(options => new(options));
        var doctorId = Guid.NewGuid();
        var quotaId = await database.InsertAsync("scheduling.DoctorDayQuota", new
        {
            DoctorId = doctorId, ServiceDate = ServiceDate.ToDateTime(TimeOnly.MinValue),
            Capacity = 30, SessionStatus = "Scheduled", IsOpen = true, HeldCount = heldCount,
            AdmittedCount = admittedCount, LockedCancelledCount = lockedCancelledCount
        });

        Assert.Equal(DoctorDayResult.HasBookings, await Service(db, Guid.NewGuid()).CloseAsync(doctorId, ServiceDate, Ct));
        Assert.True(await db.Set<DoctorDayQuota>().Where(x => x.Id == quotaId).Select(x => x.IsOpen).SingleAsync(Ct));
    }

    private static DoctorDayService Service(SchedulingDbContext db, Guid roomId) =>
        new(new DoctorDirectory(roomId), new SqlDoctorDayStore(db), new(new TimeOnly(11, 0)));

    private sealed class DoctorDirectory(Guid roomId) : IDoctorDirectory
    {
        public Task<Guid?> GetAssignedRoomIdAsync(Guid doctorId, CancellationToken ct) => Task.FromResult<Guid?>(roomId);
    }
}
