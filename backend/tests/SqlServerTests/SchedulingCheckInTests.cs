using System.Reflection;
using Clinical.Infrastructure;
using Clinical.Contracts;
using Clinical.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;
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
    [Fact]
    public async Task CheckIn_missing_specialty_rejects_without_creating_encounter_or_ticket()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock, specialtyExists: false);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            var error = await Assert.ThrowsAsync<SchedulingException>(() => CheckInAsync(services, a.AppointmentId));
            Assert.Equal(SchedulingErrorCode.InvalidAppointmentState, error.Code);
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, true);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId='{a.AppointmentId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_active_encounter_conflicts_without_changing_appointment_or_issuing_ticket()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            await using (var scope = services.CreateAsyncScope())
                await Uow(scope).ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<IClinicalModule>()
                    .StartEncounterAsync(new(a.AppointmentId, a.PatientId, day.DoctorId, day.AssignedRoomId, "TEST", "BS"), t), Ct);
            var error = await Assert.ThrowsAsync<SchedulingException>(() => CheckInAsync(services, a.AppointmentId));
            Assert.Equal(SchedulingErrorCode.InvalidAppointmentState, error.Code);
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, true);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
        }
        finally { await CleanupAttendanceAsync(day); }
    }
    [Fact]
    public async Task CheckIn_same_appointment_concurrently_creates_one_encounter_and_event()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            var results = await Task.WhenAll(CheckInAsync(services, a.AppointmentId), CheckInAsync(services, a.AppointmentId));
            Assert.Single(results, x => !x.AlreadyCheckedIn);
            Assert.Equal(results[0].QueueTicketId, results[1].QueueTicketId);
            Assert.Equal(results[0].EncounterId, results[1].EncounterId);
            await using var scope = services.CreateAsyncScope();
            Assert.Equal("Nội tổng quát", (await scope.ServiceProvider.GetRequiredService<IClinicalModule>()
                .GetEncounterByIdAsync(results[0].EncounterId, Ct))!.SpecialtySnapshot);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE EventType = 'TicketCheckedIn' AND AggregateId = '{results[0].QueueTicketId}'"));
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_waiter_and_deadline_job_recheck_time_after_acquiring_quota_lock()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? first = null;
        try
        {
            var a = await BookedAsync(services, day);
            var b = await BookedAsync(services, day);
            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            clock.NowUtc = AtIct(10, 59, 59);
            first = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async t =>
                {
                    await scope.ServiceProvider.GetRequiredService<CheckInService>().CheckInAsync(new(a.AppointmentId, Guid.NewGuid(), null), t);
                    locked.SetResult();
                    await release.Task.WaitAsync(t);
                }, Ct);
            }, Ct);
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            clock.NowUtc = AtIct(11, 0);
            var waiting = CheckInAsync(services, b.AppointmentId);
            var job = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                return await Uow(scope).ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<ICheckInStore>().MarkAbsentAsync(day.Id, t), Ct);
            }, Ct);
            try
            {
                Assert.NotSame(waiting, await Task.WhenAny(waiting, Task.Delay(BlockedWindow, Ct)));
                Assert.NotSame(job, await Task.WhenAny(job, Task.Delay(BlockedWindow, Ct)));
            }
            finally { release.TrySetResult(); }
            await first;
            var error = await Assert.ThrowsAsync<SchedulingException>(() => waiting);
            Assert.Contains(error.Code, new[] { SchedulingErrorCode.CheckInDeadlinePassed, SchedulingErrorCode.InvalidAppointmentState });
            await job;
            await AssertAttendanceAppointmentAsync(services, a.AppointmentId, AppointmentStatus.CheckedIn, true);
            await AssertAttendanceAppointmentAsync(services, b.AppointmentId, AppointmentStatus.Absent, true);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
        }
        finally
        {
            release.TrySetResult();
            if (first is not null) await first;
            await CleanupAttendanceAsync(day);
        }
    }

    [Fact]
    public async Task CheckIn_uses_configured_deadline_and_rejects_early_day()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            clock.NowUtc = AtIct(8, 0, dayOffset: -1);
            var error = await Assert.ThrowsAsync<SchedulingException>(() => CheckInAsync(services, a.AppointmentId));
            Assert.Equal(SchedulingErrorCode.CheckInNotOpen, error.Code);
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET CheckInDeadlineUtc = '{AtIct(12, 0):yyyy-MM-ddTHH:mm:ss}' WHERE Id = '{day.Id}'");
            clock.NowUtc = AtIct(11, 30);
            await using var scope = services.CreateAsyncScope();
            await AttendanceJobAsync(scope, day.Id);
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, true);
            Assert.False((await CheckInAsync(services, a.AppointmentId)).AlreadyCheckedIn);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_concurrent_receptionists_get_distinct_sequences_and_replay_once()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            var b = await BookedAsync(services, day);
            var results = await Task.WhenAll(CheckInAsync(services, a.AppointmentId), CheckInAsync(services, b.AppointmentId));
            Assert.Equal(new[] { 1, 2 }, results.Select(x => x.SequenceNumber).Order());
            Assert.Equal(2, results.Select(x => x.TicketNumber).Distinct().Count());
            var replay = await CheckInAsync(services, a.AppointmentId);
            Assert.Equal(results[0] with { AlreadyCheckedIn = true }, replay);
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId IN ('{a.AppointmentId}', '{b.AppointmentId}')"));
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE EventType = 'TicketCheckedIn' AND AggregateId IN (SELECT Id FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%')"));
            Assert.Equal(2, (await ReadQuotaAsync(services, day)).AdmittedCount);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Theory]
    [InlineData(10, 59, 59, false)]
    [InlineData(11, 0, 0, true)]
    [InlineData(11, 0, 1, true)]
    public async Task CheckIn_honors_deadline_and_keeps_admitted_count(int hour, int minute, int second, bool absent)
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            clock.NowUtc = AtIct(hour, minute, second);
            if (absent)
            {
                var error = await Assert.ThrowsAsync<SchedulingException>(() => CheckInAsync(services, a.AppointmentId));
                Assert.Equal(SchedulingErrorCode.CheckInDeadlinePassed, error.Code);
                await AssertAttendanceAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Absent, claimActive: true);
                Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
            }
            else
                Assert.Equal(1, (await CheckInAsync(services, a.AppointmentId)).SequenceNumber);
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).AdmittedCount);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckInDeadline_repeats_safely_and_preserves_checked_in_and_cancelled_appointments()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var checkedIn = await BookedAsync(services, day);
            var absent = await BookedAsync(services, day);
            var cancelled = await BookedAsync(services, day);
            await CheckInAsync(services, checkedIn.AppointmentId);
            await CancelAsync(services, PatientCancel(cancelled));
            clock.NowUtc = AtIct(11, 0);
            for (var i = 0; i < 2; i++)
            {
                await using var scope = services.CreateAsyncScope();
                await AttendanceJobAsync(scope, day.Id);
            }
            await AssertAttendanceAppointmentAsync(services, absent.AppointmentId, AppointmentStatus.Absent, claimActive: true);
            await AssertAttendanceAppointmentAsync(services, checkedIn.AppointmentId, AppointmentStatus.CheckedIn, claimActive: true);
            await AssertAppointmentAsync(services, cancelled.AppointmentId, AppointmentStatus.Cancelled, claimActive: true);
            var q = await ReadQuotaAsync(services, day);
            Assert.Equal((2, 1), (q.AdmittedCount, q.LockedCancelledCount));
            // Replay sau deadline vẫn trả vé gốc.
            Assert.True((await CheckInAsync(services, checkedIn.AppointmentId)).AlreadyCheckedIn);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_rolls_back_clinical_ticket_audit_and_appointment_when_outbox_fails()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            await using var scope = services.CreateAsyncScope();
            var service = ActivatorUtilities.CreateInstance<CheckInService>(scope.ServiceProvider, new FailingCheckInPublisher());
            await Assert.ThrowsAsync<RollbackSignal>(() => service.CheckInAsync(new(a.AppointmentId, Guid.NewGuid(), null), Ct));
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.AuditLog WHERE RecordId = '{a.AppointmentId}'"));
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_rolls_back_appointment_ticket_and_encounter_when_clinical_fails_after_writing()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            await using var scope = services.CreateAsyncScope();
            // Clinical ghi Encounter thật rồi lỗi: Encounter phải rollback cùng transaction của Scheduling.
            var clinical = FailingAfterWriteClinical.Create(scope.ServiceProvider.GetRequiredService<IClinicalModule>());
            var service = ActivatorUtilities.CreateInstance<CheckInService>(scope.ServiceProvider, (IClinicalModule)clinical);
            await Assert.ThrowsAsync<RollbackSignal>(() => service.CheckInAsync(new(a.AppointmentId, Guid.NewGuid(), null), Ct));
            Assert.True(clinical.Wrote);
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE EventType = 'TicketCheckedIn' AND PayloadJson LIKE '%{a.AppointmentId}%'"));
            // Sau lỗi, check-in lại vẫn thành công: không còn Encounter mồ côi chặn ActiveEncounter.
            Assert.Equal(1, (await CheckInAsync(services, a.AppointmentId)).SequenceNumber);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Absent")]
    [InlineData("Completed")]
    [InlineData("Rescheduled")]
    public async Task CheckIn_rejects_terminal_states(string status)
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            await ExecAsync($"UPDATE scheduling.Appointment SET Status = '{status}' WHERE Id = '{a.AppointmentId}'");
            var error = await Assert.ThrowsAsync<SchedulingException>(() => CheckInAsync(services, a.AppointmentId));
            Assert.Equal(SchedulingErrorCode.InvalidAppointmentState, error.Code);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE AppointmentId = '{a.AppointmentId}'"));
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    [Fact]
    public async Task CheckIn_today_uses_vietnam_date_and_server_filters()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = AttendanceServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            var a = await BookedAsync(services, day);
            // 00:30 VN ngày D vẫn là ngày D-1 ở UTC.
            clock.NowUtc = AtIct(0, 30);
            await using var scope = services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<ICheckInStore>();
            var row = Assert.Single(await store.ListTodayAsync(day.DoctorId, null, 0, 51, Ct));
            Assert.Equal(a.AppointmentId, row.AppointmentId);
            Assert.Empty(await store.ListTodayAsync(Guid.NewGuid(), null, 0, 51, Ct));
            Assert.Empty(await store.ListTodayAsync(day.DoctorId, "missing", 0, 51, Ct));
            Assert.Single(await store.ListTodayAsync(day.DoctorId, row.AppointmentNumber, 0, 51, Ct));
            await AttendanceJobAsync(scope, day.Id);
            await AssertAppointmentAsync(services, a.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
        }
        finally { await CleanupAttendanceAsync(day); }
    }

    private static readonly Guid AttendanceSpecialtyId = Guid.NewGuid();

    private static ServiceProvider AttendanceServices(BoundaryDbClock clock, bool specialtyExists = true)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var identity = DispatchProxy.Create<IIdentityPatientsModule, AttendanceIdentity>();
        ((AttendanceIdentity)(object)identity).SpecialtyExists = specialtyExists;
        return new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs).AddClinicalInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddUnitOfWorkParticipant<ClinicalDbContext>().AddSchedulingApplication()
            .AddSingleton<Billing.Contracts.IBillingModule>(new FixedFeeBilling())
            .AddSingleton<Billing.Contracts.IPaymentRefunds>(new RecordingRefunds())
            .AddSingleton<IDbClock>(clock)
            .AddSingleton(identity)
            .BuildServiceProvider(validateScopes: true);
    }

    private static async Task AssertAttendanceAppointmentAsync(ServiceProvider services, Guid id, AppointmentStatus status, bool claimActive)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var a = await db.Set<Appointment>().SingleAsync(x => x.Id == id, Ct);
        Assert.Equal(status, a.Status);
        Assert.Equal(claimActive, await db.Set<ActiveBookingClaim>().Where(x => x.AppointmentId == id).Select(x => x.IsActive).SingleAsync(Ct));
        Assert.Equal(ReservationStatus.Confirmed, await db.Set<Reservation>().Where(x => x.Id == a.ReservationId).Select(x => x.Status).SingleAsync(Ct));
    }

    // Chỉ cho job nhận quota của test này, tránh job dùng fake clock 2099 sửa dữ liệu test khác.
    private static Task AttendanceJobAsync(AsyncServiceScope scope, Guid id) =>
        new CheckInDeadlineJob(new AttendanceJobStore(scope.ServiceProvider.GetRequiredService<ICheckInStore>(), id), Uow(scope)).RunAsync(Ct);

    private sealed class AttendanceJobStore(ICheckInStore inner, Guid id) : ICheckInStore
    {
        public Task<CheckInOutcome> CheckInAsync(CheckInAppointment c, Func<AppointmentForCheckIn, CancellationToken, Task<Guid>> open, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<TodayAppointment>> ListTodayAsync(Guid? d, string? n, int skip, int take, CancellationToken ct) => throw new NotSupportedException();
        public async Task<IReadOnlyList<Guid>> FindOverdueQuotasAsync(int take, CancellationToken ct) =>
            (await inner.FindOverdueQuotasAsync(take, ct)).Where(x => x == id).ToArray();
        public Task<int> MarkAbsentAsync(Guid quotaId, CancellationToken ct) => inner.MarkAbsentAsync(quotaId, ct);
    }

    public class AttendanceIdentity : DispatchProxy
    {
        public bool SpecialtyExists { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "GetDoctorAsync" => Task.FromResult<DoctorDto?>(new((Guid)args![0]!, "BS Test", AttendanceSpecialtyId, "TEST", Guid.NewGuid(), "BS", true)),
            "GetSpecialtiesAsync" => Task.FromResult<IReadOnlyList<SpecialtyDto>>(SpecialtyExists ? [new(AttendanceSpecialtyId, "TEST", "Nội tổng quát")] : []),
            _ => throw new NotSupportedException(method?.Name)
        };
    }

    // Gọi StartEncounterAsync thật (ghi Encounter) rồi ném lỗi; method khác chuyển thẳng cho module thật.
    public class FailingAfterWriteClinical : DispatchProxy
    {
        public IClinicalModule Inner { get; set; } = null!;
        public bool Wrote { get; private set; }

        public static FailingAfterWriteClinical Create(IClinicalModule inner)
        {
            var proxy = (FailingAfterWriteClinical)(object)DispatchProxy.Create<IClinicalModule, FailingAfterWriteClinical>();
            proxy.Inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IClinicalModule.StartEncounterAsync)
                ? FailAsync((StartEncounterCommand)args![0]!, (CancellationToken)args[1]!)
                : method.Invoke(Inner, args);

        private async Task<EncounterDto> FailAsync(StartEncounterCommand command, CancellationToken ct)
        {
            await Inner.StartEncounterAsync(command, ct);
            Wrote = true;
            throw new RollbackSignal();
        }
    }

    private sealed class FailingCheckInPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync<T>(T e, Guid id, CancellationToken ct) where T : IIntegrationEvent => throw new RollbackSignal();
    }

    private static async Task<CheckInResult> CheckInAsync(ServiceProvider services, Guid appointmentId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CheckInService>().CheckInAsync(new(appointmentId, Guid.NewGuid(), null), Ct);
    }

    private static async Task CleanupAttendanceAsync(params DoctorDayQuota[] days)
    {
        var ids = string.Join(", ", days.Select(d => $"'{d.Id}'"));
        await ExecAsync($"""
            DECLARE @encounters TABLE (Id uniqueidentifier);
            INSERT INTO @encounters SELECT Id FROM clinical.Encounter WHERE AppointmentId IN
                (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId IN ({ids}));
            DELETE FROM shared.OutboxMessage WHERE AggregateId IN (SELECT Id FROM scheduling.QueueTicket WHERE EncounterId IN (SELECT Id FROM @encounters));
            DELETE FROM scheduling.QueueTicket WHERE EncounterId IN (SELECT Id FROM @encounters);
            DELETE FROM clinical.EncounterClinicalRecord WHERE EncounterId IN (SELECT Id FROM @encounters);
            DELETE FROM clinical.Encounter WHERE Id IN (SELECT Id FROM @encounters);
            """);
        await CleanupAsync(days);
    }
}
