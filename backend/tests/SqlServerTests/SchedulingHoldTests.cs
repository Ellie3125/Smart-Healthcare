using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Confirmations;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

// W2-02: commit thật vào SmartHealthcare_Test rồi tự dọn; chạy chung collection không song song với các test commit khác.
[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed partial class SchedulingHoldTests
{
    private static readonly DateOnly ServiceDate = new(2099, 1, 15);
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromSeconds(1);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hundred_concurrent_holds_on_thirty_slots_get_exactly_thirty()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services, capacity: 30);
        try
        {
            var patients = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
            var attempts = new Guid[patients.Length];
            for (var i = 0; i < patients.Length; i++)
                attempts[i] = await SeedAttemptAsync(services, patients[i]);

            var results = await Task.WhenAll(patients.Select((patient, i) => Task.Run(() => HoldAsync(services, day, attempts[i], patient), Ct)));

            Assert.Equal(30, results.Count(x => x.Result == HoldResult.Held));
            Assert.Equal(70, results.Count(x => x.Result == HoldResult.Full));
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((30, 0, 0), (quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount));
            Assert.Equal(30, await CountAsync($"SELECT COUNT(*) FROM scheduling.Reservation WHERE SessionQuotaId = '{day.Id}' AND Status = 'Held'"));
            Assert.Equal(30, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveBookingClaim WHERE SessionQuotaId = '{day.Id}' AND IsActive = 1"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Hold_waits_for_close_and_then_sees_the_day_closed()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);
            var closed = new TaskCompletionSource<DoctorDayResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var close = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async t =>
                {
                    closed.SetResult(await scope.ServiceProvider.GetRequiredService<IDoctorDayStore>().CloseAsync(day.DoctorId, ServiceDate, t));
                    await release.Task; // giữ khóa quota, chưa commit
                }, Ct);
            }, Ct);
            Assert.Equal(DoctorDayResult.Closed, await closed.Task);

            var hold = Task.Run(() => HoldAsync(services, day, attempt, patient), Ct);
            Assert.NotSame(hold, await Task.WhenAny(hold, Task.Delay(BlockedWindow, Ct)));

            release.SetResult();
            await close;
            Assert.Equal(HoldResult.DayClosed, (await hold).Result);
            var quota = await ReadQuotaAsync(services, day);
            Assert.False(quota.IsOpen);
            Assert.Equal(0, quota.HeldCount);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Close_waits_for_hold_and_then_sees_the_held_booking()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);
            var held = new TaskCompletionSource<HoldOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var hold = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async t =>
                {
                    held.SetResult(await scope.ServiceProvider.GetRequiredService<IHoldStore>()
                        .HoldAsync(Request(day, attempt, patient), HoldService.HoldDuration, t));
                    await release.Task; // giữ khóa quota, chưa commit
                }, Ct);
            }, Ct);
            Assert.Equal(HoldResult.Held, (await held.Task).Result);

            var close = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IDoctorDayStore>().CloseAsync(day.DoctorId, ServiceDate, Ct);
            }, Ct);
            Assert.NotSame(close, await Task.WhenAny(close, Task.Delay(BlockedWindow, Ct)));

            release.SetResult();
            await hold;
            Assert.Equal(DoctorDayResult.HasBookings, await close);
            var quota = await ReadQuotaAsync(services, day);
            Assert.True(quota.IsOpen);
            Assert.Equal(1, quota.HeldCount);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Held_creates_reservation_claim_and_updates_attempt()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);

            var outcome = await HoldAsync(services, day, attempt, patient);

            Assert.Equal(HoldResult.Held, outcome.Result);
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var reservation = await db.Set<Reservation>().SingleAsync(x => x.Id == outcome.ReservationId, Ct);
            Assert.Equal((ReservationStatus.Held, attempt, patient), (reservation.Status, reservation.BookingAttemptId, reservation.PatientId));
            Assert.Equal(HoldService.HoldDuration, reservation.ExpiresAtUtc - reservation.ReservedAtUtc);
            Assert.Equal(outcome.ExpiresAtUtc, reservation.ExpiresAtUtc);
            var claim = await db.Set<ActiveBookingClaim>().SingleAsync(x => x.ReservationId == reservation.Id, Ct);
            Assert.True(claim.IsActive);
            Assert.Equal((day.DoctorId, ServiceDate, ClaimSource.Web), (claim.DoctorId, claim.ServiceDate, claim.ClaimSource));
            var saved = await db.Set<BookingAttempt>().SingleAsync(x => x.Id == attempt, Ct);
            Assert.Equal((BookingAttemptStatus.Held, CurrentStep.SlotHeld, day.DoctorId, ServiceDate),
                (saved.Status, saved.CurrentStep, saved.SelectedDoctorId, saved.SelectedServiceDate));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Rejected_holds_leave_no_changes()
    {
        await using var services = BuildServices();
        var open = await SeedDayAsync(services);
        var closed = await SeedDayAsync(services, isOpen: false);
        var pastCutoff = await SeedDayAsync(services, cutoffFromNow: TimeSpan.FromMinutes(-1));
        try
        {
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);
            var other = await SeedAttemptAsync(services, patient);

            Assert.Equal(HoldResult.DayNotFound, (await HoldAsync(services, new DoctorDayQuota { DoctorId = Guid.NewGuid() }, attempt, patient)).Result);
            Assert.Equal(HoldResult.DayClosed, (await HoldAsync(services, closed, attempt, patient)).Result);
            Assert.Equal(HoldResult.CutoffPassed, (await HoldAsync(services, pastCutoff, attempt, patient)).Result);
            Assert.Equal(HoldResult.AttemptNotFound, (await HoldAsync(services, open, attempt, Guid.NewGuid())).Result);
            Assert.Equal(HoldResult.InvalidInput, (await HoldAsync(services, open, Guid.Empty, patient)).Result);

            Assert.Equal(HoldResult.Held, (await HoldAsync(services, open, attempt, patient)).Result);
            Assert.Equal(HoldResult.InvalidAttemptState, (await HoldAsync(services, open, attempt, patient)).Result);
            Assert.Equal(HoldResult.AlreadyClaimed, (await HoldAsync(services, open, other, patient)).Result);

            Assert.Equal(1, (await ReadQuotaAsync(services, open)).HeldCount);
            Assert.Equal(0, (await ReadQuotaAsync(services, closed)).HeldCount);
            Assert.Equal(0, (await ReadQuotaAsync(services, pastCutoff)).HeldCount);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Reservation WHERE PatientId = '{patient}'"));
        }
        finally
        {
            await CleanupAsync(open, closed, pastCutoff);
        }
    }

    [Fact]
    public async Task Expiry_releases_the_slot_once_and_skips_holds_not_yet_due()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (duePatient, livePatient) = (Guid.NewGuid(), Guid.NewGuid());
            var dueAttempt = await SeedAttemptAsync(services, duePatient);
            var due = await HoldAsync(services, day, dueAttempt, duePatient);
            var live = await HoldAsync(services, day, await SeedAttemptAsync(services, livePatient), livePatient);
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = '{due.ReservationId}'");

            await using (var scope = services.CreateAsyncScope())
                Assert.True(await scope.ServiceProvider.GetRequiredService<HoldService>().ExpireDueAsync(Ct) >= 1);

            await using (var scope = services.CreateAsyncScope())
            {
                // Job chạy lặp hoặc hai worker cùng nhận: không trừ HeldCount lần nữa.
                var store = scope.ServiceProvider.GetRequiredService<IHoldStore>();
                Assert.False(await Uow(scope).ExecuteAsync(t => store.ExpireAsync(due.ReservationId!.Value, t), Ct));
                Assert.False(await Uow(scope).ExecuteAsync(t => store.ExpireAsync(live.ReservationId!.Value, t), Ct));

                var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
                Assert.Equal(ReservationStatus.Expired, await db.Set<Reservation>().Where(x => x.Id == due.ReservationId).Select(x => x.Status).SingleAsync(Ct));
                Assert.Equal(ReservationStatus.Held, await db.Set<Reservation>().Where(x => x.Id == live.ReservationId).Select(x => x.Status).SingleAsync(Ct));
                var claim = await db.Set<ActiveBookingClaim>().SingleAsync(x => x.ReservationId == due.ReservationId, Ct);
                Assert.False(claim.IsActive);
                Assert.NotNull(claim.ReleasedAtUtc);
                var attempt = await db.Set<BookingAttempt>().SingleAsync(x => x.Id == dueAttempt, Ct);
                Assert.Equal((BookingAttemptStatus.NeedsReselection, CurrentStep.DoctorSelection), (attempt.Status, attempt.CurrentStep));
            }

            Assert.Equal(1, (await ReadQuotaAsync(services, day)).HeldCount);
            // Claim đã nhả nên bệnh nhân giữ chỗ lại được bằng cùng BookingAttempt.
            Assert.Equal(HoldResult.Held, (await HoldAsync(services, day, dueAttempt, duePatient)).Result);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // ---------- W2-03: idempotency + Operation ----------

    [Fact]
    public async Task Create_attempt_replays_the_same_id_and_rejects_a_different_body()
    {
        await using var services = BuildServices();
        var (user, patient, key) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        try
        {
            var first = await CreateAttemptAsync(services, new(user, patient, ServiceDate, key));
            await using var restarted = BuildServices(); // process mới
            var again = await CreateAttemptAsync(restarted, new(user, patient, ServiceDate, key));
            var otherBody = await CreateAttemptAsync(services, new(user, Guid.NewGuid(), ServiceDate, key));

            Assert.Equal(CreateAttemptResult.Created, first.Result);
            Assert.Equal(first, again);
            Assert.Equal(CreateAttemptResult.IdempotencyConflict, otherBody.Result);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.BookingAttempt WHERE AppUserId = '{user}'"));
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task Hold_resent_after_commit_returns_the_same_operation_and_holds_once()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var request = Request(day, await SeedAttemptAsync(services, patient), patient);

            var first = await HoldAsync(services, request);
            // Mất phản hồi sau commit: client gửi lại cùng khóa tới process khác (BE2, hoặc BE1 đã khởi động lại).
            await using var restarted = BuildServices();
            var resent = await HoldAsync(restarted, request);
            var conflict = await HoldAsync(restarted, request with { DoctorId = Guid.NewGuid() });

            Assert.Equal(HoldResult.Held, first.Result);
            Assert.NotNull(first.OperationId);
            Assert.Equal(first, resent);
            Assert.Equal(HoldResult.IdempotencyConflict, conflict.Result);
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).HeldCount);
            var operation = await GetOperationAsync(restarted, first.OperationId!.Value);
            Assert.NotNull(operation);
            Assert.Equal((OperationType.HoldSlot, OperationStatus.Succeeded, request.BookingAttemptId, request.AppUserId),
                (operation.OperationType, operation.Status, operation.BookingAttemptId, operation.InitiatedByUserId));
            Assert.NotNull(operation.CompletedAtUtc);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM workflow.Operation WHERE BookingAttemptId = '{request.BookingAttemptId}'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Concurrent_duplicates_with_the_same_key_hold_once()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var request = Request(day, await SeedAttemptAsync(services, patient), patient);

            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => HoldAsync(services, request), Ct)));

            Assert.All(results, x => Assert.Equal(results[0], x));
            Assert.Equal(HoldResult.Held, results[0].Result);
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).HeldCount);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM workflow.Operation WHERE BookingAttemptId = '{request.BookingAttemptId}'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Failure_before_commit_leaves_nothing_and_the_resend_runs_fresh()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var patient = Guid.NewGuid();
            var request = Request(day, await SeedAttemptAsync(services, patient), patient);
            HoldOutcome? lost = null;

            await using (var scope = services.CreateAsyncScope())
            {
                // Lệnh chạy lồng trong transaction ngoài rồi tiến trình chết trước commit.
                await Assert.ThrowsAsync<RollbackSignal>(() => Uow(scope).ExecuteAsync(async t =>
                {
                    lost = await scope.ServiceProvider.GetRequiredService<HoldService>().HoldAsync(request, t);
                    throw new RollbackSignal();
                }, Ct));
            }

            Assert.Equal(HoldResult.Held, lost!.Result);
            Assert.Equal(0, (await ReadQuotaAsync(services, day)).HeldCount);
            Assert.Null(await GetOperationAsync(services, lost.OperationId!.Value));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.ApiIdempotencyRecord WHERE OperationId = '{lost.OperationId}'"));

            var resent = await HoldAsync(services, request);
            Assert.Equal(HoldResult.Held, resent.Result);
            Assert.NotEqual(lost.OperationId, resent.OperationId);
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).HeldCount);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Rejected_hold_is_recorded_and_replayed_even_after_the_day_reopens()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services, isOpen: false);
        try
        {
            var patient = Guid.NewGuid();
            var request = Request(day, await SeedAttemptAsync(services, patient), patient);

            var first = await HoldAsync(services, request);
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET IsOpen = 1 WHERE Id = '{day.Id}'");
            var resent = await HoldAsync(services, request);
            var newKey = await HoldAsync(services, request with { IdempotencyKey = Guid.NewGuid().ToString("N") });

            Assert.Equal(HoldResult.DayClosed, first.Result);
            Assert.Equal(first, resent);
            var operation = await GetOperationAsync(services, first.OperationId!.Value);
            Assert.Equal((OperationStatus.NeedsAction, "DayClosed"), (operation!.Status, operation.FailureReason));
            Assert.Equal(HoldResult.Held, newKey.Result); // chọn lại là thao tác mới với khóa mới
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // ---------- helpers ----------

    private sealed class RollbackSignal : Exception;

    private static ServiceProvider BuildServices()
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection()
            .AddSharedInfrastructure(cs)
            .AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddSchedulingApplication()
            .AddSingleton<Billing.Contracts.IBillingModule>(new FixedFeeBilling())
            .BuildServiceProvider(validateScopes: true);
    }

    private static IUnitOfWork Uow(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    private static HoldRequest Request(DoctorDayQuota day, Guid attempt, Guid patient) =>
        new(attempt, patient, Guid.NewGuid(), day.DoctorId, Guid.NewGuid(), ServiceDate, BookingChannel.Web, Guid.NewGuid().ToString("N"));

    private static Task<HoldOutcome> HoldAsync(ServiceProvider services, DoctorDayQuota day, Guid attempt, Guid patient) =>
        HoldAsync(services, Request(day, attempt, patient));

    private static async Task<HoldOutcome> HoldAsync(ServiceProvider services, HoldRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HoldService>().HoldAsync(request, Ct);
    }

    private static async Task<CreateAttemptOutcome> CreateAttemptAsync(ServiceProvider services, CreateAttemptRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BookingAttemptService>().CreateAsync(request, Ct);
    }

    private static async Task<OperationView?> GetOperationAsync(ServiceProvider services, Guid operationId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOperationTracker>().GetAsync(operationId, Ct);
    }

    private static async Task<DoctorDayQuota> SeedDayAsync(ServiceProvider services, int capacity = 30, bool isOpen = true,
        TimeSpan? cutoffFromNow = null, TimeSpan? rescheduleCutoffFromNow = null)
    {
        await using var scope = services.CreateAsyncScope();
        var now = await scope.ServiceProvider.GetRequiredService<IDbClock>().GetUtcNowAsync(Ct);
        var quota = new DoctorDayQuota
        {
            Id = Guid.CreateVersion7(),
            DoctorId = Guid.NewGuid(),
            AssignedRoomId = Guid.NewGuid(),
            ServiceDate = ServiceDate,
            Capacity = capacity,
            SessionStartTimeUtc = now.AddHours(2),
            BookingCutoffTimeUtc = now + (cutoffFromNow ?? TimeSpan.FromHours(1)),
            RescheduleCutoffTimeUtc = now + (rescheduleCutoffFromNow ?? TimeSpan.FromMinutes(30)),
            SessionStatus = SessionStatus.Scheduled,
            IsOpen = isOpen,
        };
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        db.Add(quota);
        await db.SaveChangesAsync(Ct);
        return quota;
    }

    private static async Task<Guid> SeedAttemptAsync(ServiceProvider services, Guid patient)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var attempt = new BookingAttempt
        {
            Id = Guid.CreateVersion7(),
            AppUserId = Guid.NewGuid(),
            PatientId = patient,
            CurrentStep = CurrentStep.DoctorSelection,
            SelectedServiceDate = ServiceDate,
            Status = BookingAttemptStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Add(attempt);
        await db.SaveChangesAsync(Ct);
        return attempt.Id;
    }

    private static async Task<DoctorDayQuota> ReadQuotaAsync(ServiceProvider services, DoctorDayQuota day)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<DoctorDayQuota>()
            .AsNoTracking().SingleAsync(x => x.Id == day.Id, Ct);
    }

    private static async Task<int> CountAsync(string sql)
    {
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }

    // Mọi dữ liệu test dùng ServiceDate 2099-01-15: dọn theo ngày đó, kèm Operation và bản ghi idempotency liên quan.
    private static Task CleanupAsync(params DoctorDayQuota[] days)
    {
        var ids = days.Length == 0 ? "NULL" : string.Join(", ", days.Select(d => $"'{d.Id}'"));
        return ExecAsync($"""
            DECLARE @attempts TABLE (Id uniqueidentifier, AppUserId uniqueidentifier);
            INSERT INTO @attempts SELECT Id, AppUserId FROM scheduling.BookingAttempt WHERE SelectedServiceDate = '{ServiceDate:yyyy-MM-dd}';
            DECLARE @ops TABLE (Id uniqueidentifier);
            INSERT INTO @ops SELECT Id FROM workflow.Operation WHERE BookingAttemptId IN (SELECT Id FROM @attempts)
              OR TargetEntityId IN (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId IN ({ids}));
            DELETE FROM shared.ApiIdempotencyRecord WHERE OperationId IN (SELECT Id FROM @ops)
              OR CallerScope IN (SELECT 'AppUser:' + LOWER(CONVERT(nvarchar(36), AppUserId)) FROM @attempts);
            DELETE FROM workflow.Operation WHERE Id IN (SELECT Id FROM @ops);
            DELETE FROM shared.OutboxMessage WHERE AggregateId IN (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId IN ({ids}));
            DELETE FROM shared.ProcessedEvent WHERE HandlerName = '{PaymentVerifiedHandler.HandlerName}';
            DELETE FROM scheduling.ActiveBookingClaim WHERE SessionQuotaId IN ({ids});
            DELETE FROM shared.AuditLog WHERE RecordId IN (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId IN ({ids}));
            UPDATE scheduling.Appointment SET RescheduledFromAppointmentId = NULL WHERE SessionQuotaId IN ({ids});
            DELETE FROM scheduling.Appointment WHERE SessionQuotaId IN ({ids});
            DELETE FROM scheduling.Reservation WHERE SessionQuotaId IN ({ids}) OR BookingAttemptId IN (SELECT Id FROM @attempts);
            DELETE FROM scheduling.BookingAttempt WHERE Id IN (SELECT Id FROM @attempts);
            DELETE FROM scheduling.DoctorDayQuota WHERE Id IN ({ids});
            """);
    }

    private static async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
