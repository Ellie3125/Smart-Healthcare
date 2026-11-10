using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.Appointments;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

public sealed partial class SchedulingHoldTests
{
    // Phạm vi v2.12 §4.2: xét 09:30 của ngày phục vụ, kể cả khi đã qua mốc của hôm nay.
    [Theory]
    [InlineData(0, 9, 29, 59, HoldResult.Held)]
    [InlineData(0, 9, 30, 0, HoldResult.CutoffPassed)]
    [InlineData(-1, 9, 30, 1, HoldResult.Held)]
    public async Task Hold_uses_the_cutoff_of_the_requested_service_date(
        int dayOffset, int hour, int minute, int second, HoldResult expected)
    {
        var clock = new BoundaryDbClock(AtIct(hour, minute, second, dayOffset));
        await using var services = BuildBoundaryServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);

            var result = await HoldAsync(services, day, attempt, patient);

            var expectedHolds = expected == HoldResult.Held ? 1 : 0;
            Assert.Equal(expected, result.Result);
            Assert.Equal(expectedHolds, (await ReadQuotaAsync(services, day)).HeldCount);
            Assert.Equal(expectedHolds,
                await CountAsync($"SELECT COUNT(*) FROM scheduling.Reservation WHERE SessionQuotaId = '{day.Id}'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // Phạm vi v2.12 §4.2: giữ 09:28 có TTL 5 phút đến 09:33, xác nhận lúc 09:32 vẫn hợp lệ.
    [Fact]
    public async Task Hold_at_0928_expires_at_0933_and_can_be_confirmed_at_0932()
    {
        var clock = new BoundaryDbClock(AtIct(9, 28));
        await using var services = BuildBoundaryServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var (attempt, held) = await HeldAttemptAsync(services, day);
            Assert.Equal(AtIct(9, 33), held.ExpiresAtUtc);

            clock.NowUtc = AtIct(9, 32);
            await HandleAsync(services, Paid(attempt) with { OccurredAtUtc = clock.NowUtc });

            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 1), (quota.HeldCount, quota.AdmittedCount));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Appointment WHERE ReservationId = '{held.ReservationId}' AND Status = 'Scheduled'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // Phạm vi v2.12 §6.2: hủy lúc 04:59:59 trả suất; từ đúng 05:00:00 khóa suất.
    [Theory]
    [InlineData(4, 59, 59, true)]
    [InlineData(5, 0, 0, false)]
    public async Task Cancellation_releases_or_locks_the_slot_at_0500(int hour, int minute, int second, bool releases)
    {
        var clock = new BoundaryDbClock(AtIct(4, 55));
        await using var services = BuildBoundaryServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var booked = await BookedAsync(services, day);
            clock.NowUtc = AtIct(hour, minute, second);

            var outcome = await CancelAsync(services, PatientCancel(booked));

            Assert.Equal(ChangeResult.Cancelled, outcome.Result);
            Assert.Equal(releases, outcome.Cancelled!.SlotReleased);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, releases ? 0 : 1), (quota.AdmittedCount, quota.LockedCancelledCount));
            await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Cancelled, claimActive: !releases);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // Phạm vi v2.12 §6.1: trước 05:00 chuyển suất khi đổi thành công; từ 05:00 giữ nguyên lịch cũ.
    [Theory]
    [InlineData(4, 59, 59, ChangeResult.Rescheduled, 0, 1)]
    [InlineData(5, 0, 0, ChangeResult.RescheduleCutoffPassed, 1, 0)]
    public async Task Reschedule_moves_the_slot_only_before_0500(
        int hour, int minute, int second, ChangeResult expected, int sourceAdmitted, int targetAdmitted)
    {
        var clock = new BoundaryDbClock(AtIct(4, 55));
        await using var services = BuildBoundaryServices(clock);
        var source = await SeedBoundaryDayAsync(services, clock);
        DoctorDayQuota? target = null;
        try
        {
            target = await SeedBoundaryDayAsync(services, clock);
            var booked = await BookedAsync(services, source);
            clock.NowUtc = AtIct(hour, minute, second);

            var outcome = await RescheduleAsync(services, Reschedule(booked, target));

            Assert.Equal(expected, outcome.Result);
            var originalQuota = await ReadQuotaAsync(services, source);
            Assert.Equal((sourceAdmitted, 0), (originalQuota.AdmittedCount, originalQuota.LockedCancelledCount));
            Assert.Equal(targetAdmitted, (await ReadQuotaAsync(services, target)).AdmittedCount);
            if (expected == ChangeResult.Rescheduled)
            {
                await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Rescheduled, claimActive: false);
                await AssertAppointmentAsync(services, outcome.Rescheduled!.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
            }
            else
            {
                await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
            }
        }
        finally
        {
            await CleanupAsync(target is null ? [source] : [source, target]);
        }
    }

    // Phạm vi v2.12 §4.3: hai yêu cầu tranh suất cuối phải có đúng một giữ chỗ.
    [Fact]
    public async Task Two_requests_contend_for_the_last_slot_on_sql_server()
    {
        var clock = new BoundaryDbClock(AtIct(9, 29, 59));
        await using var services = BuildBoundaryServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock, capacity: 1);
        try
        {
            var patientA = Guid.NewGuid();
            var patientB = Guid.NewGuid();
            var attemptA = await SeedAttemptAsync(services, patientA);
            var attemptB = await SeedAttemptAsync(services, patientB);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = ContendAsync(attemptA, patientA);
            var second = ContendAsync(attemptB, patientB);
            start.SetResult();
            var outcomes = await Task.WhenAll(first, second);

            Assert.Single(outcomes, x => x.Result == HoldResult.Held);
            Assert.Single(outcomes, x => x.Result == HoldResult.Full);
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).HeldCount);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Reservation WHERE SessionQuotaId = '{day.Id}' AND Status = 'Held'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveBookingClaim WHERE SessionQuotaId = '{day.Id}' AND IsActive = 1"));

            async Task<HoldOutcome> ContendAsync(Guid attemptId, Guid patientId)
            {
                await start.Task.WaitAsync(Ct);
                return await HoldAsync(services, day, attemptId, patientId);
            }
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // Phạm vi v2.12 §6.2: hủy và trả suất chỉ tác động một lần, kể cả replay sau mốc 05:00.
    [Fact]
    public async Task Replaying_cancellation_across_0500_releases_the_slot_once()
    {
        var clock = new BoundaryDbClock(AtIct(4, 55));
        await using var services = BuildBoundaryServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var booked = await BookedAsync(services, day);
            var other = await BookedAsync(services, day);
            var request = PatientCancel(booked);

            clock.NowUtc = AtIct(4, 59, 59);
            var cancelled = await CancelAsync(services, request);
            clock.NowUtc = AtIct(5, 0);
            var replay = await CancelAsync(services, request);

            Assert.Equal(ChangeResult.Cancelled, cancelled.Result);
            Assert.True(cancelled.Cancelled!.SlotReleased);
            Assert.NotNull(cancelled.OperationId);
            Assert.Equal(cancelled, replay);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((1, 0), (quota.AdmittedCount, quota.LockedCancelledCount));
            await AssertAppointmentAsync(services, other.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{booked.AppointmentId}' AND EventType = 'AppointmentCancelled'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    private static DateTime AtIct(int hour, int minute, int second = 0, int dayOffset = 0) =>
        new DateTimeOffset(ServiceDate.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute, second)),
            TimeSpan.FromHours(7)).UtcDateTime;

    private static Task<DoctorDayQuota> SeedBoundaryDayAsync(ServiceProvider services, BoundaryDbClock clock, int capacity = 30) =>
        SeedDayAsync(services, capacity, cutoffFromNow: AtIct(9, 30) - clock.NowUtc,
            rescheduleCutoffFromNow: AtIct(5, 0) - clock.NowUtc);

    private static ServiceProvider BuildBoundaryServices(BoundaryDbClock clock)
    {
        var connectionString = TestDatabase.RequiredConnectionString();
        return new ServiceCollection()
            .AddSharedInfrastructure(connectionString)
            .AddSchedulingInfrastructure(connectionString)
            .AddNotificationsInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddSchedulingApplication()
            .AddSingleton<Billing.Contracts.IBillingModule>(new FixedFeeBilling())
            .AddSingleton<IDbClock>(clock)
            .BuildServiceProvider(validateScopes: true);
    }

    private sealed class BoundaryDbClock(DateTime nowUtc) : IDbClock
    {
        public DateTime NowUtc { get; set; } = nowUtc;
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(NowUtc);
    }
}
