using Billing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Scheduling.Application.Confirmations;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

// W2-06: handler PaymentVerified gọi trực tiếp (dispatcher Outbox ở W3-00).
public sealed partial class SchedulingHoldTests
{
    private const decimal ExamFee = 150_000m;
    private static readonly Guid ExpensiveRank = Guid.NewGuid(); // FixedFeeBilling báo 300000 cho hạng này

    [Fact]
    public async Task Payment_confirms_the_hold_once_and_publishes_appointment_confirmed()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (attempt, held) = await HeldAttemptAsync(services, day);
            var paid = Paid(attempt);

            await HandleAsync(services, paid);
            await HandleAsync(services, paid); // Outbox giao lặp cùng EventId
            await HandleAsync(services, Paid(attempt)); // sự kiện khác của cùng giao dịch

            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 1), (quota.HeldCount, quota.AdmittedCount));
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var appointment = await db.Set<Appointment>().SingleAsync(x => x.ReservationId == held.ReservationId, Ct);
            Assert.Equal((AppointmentStatus.Scheduled, ExamFee, quota.Id), (appointment.Status, appointment.AppliedConsultationFee, appointment.SessionQuotaId));
            Assert.Matches(@"^LH990115-[0-9A-F]{8}$", appointment.AppointmentNumber);
            Assert.Equal(ReservationStatus.Confirmed, await db.Set<Reservation>().Where(x => x.Id == held.ReservationId).Select(x => x.Status).SingleAsync(Ct));
            var claim = await db.Set<ActiveBookingClaim>().SingleAsync(x => x.ReservationId == held.ReservationId, Ct);
            Assert.True(claim.IsActive);
            Assert.Equal(appointment.Id, claim.AppointmentId);
            var saved = await db.Set<BookingAttempt>().SingleAsync(x => x.Id == attempt, Ct);
            Assert.Equal((BookingAttemptStatus.Confirmed, CurrentStep.Finalized), (saved.Status, saved.CurrentStep));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{appointment.Id}' AND EventType = 'AppointmentConfirmed'"));
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM shared.ProcessedEvent WHERE HandlerName = '{PaymentVerifiedHandler.HandlerName}'"));

            // Đã xác nhận thì hết hạn không trả suất lần nữa.
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = '{held.ReservationId}'");
            await using var expireScope = services.CreateAsyncScope();
            await expireScope.ServiceProvider.GetRequiredService<HoldService>().ExpireDueAsync(Ct);
            Assert.Equal((0, 1), ((await ReadQuotaAsync(services, day)).HeldCount, (await ReadQuotaAsync(services, day)).AdmittedCount));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Theory]
    [InlineData(true)]  // job hết hạn đã chạy trước khi tiền về
    [InlineData(false)] // quá ExpiresAt nhưng job chưa chạy
    public async Task Late_payment_records_paid_unallocated_without_an_appointment(bool expiredByJob)
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (attempt, held) = await HeldAttemptAsync(services, day);
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = '{held.ReservationId}'");
            if (expiredByJob)
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<HoldService>().ExpireDueAsync(Ct);
            }

            await HandleAsync(services, Paid(attempt));

            Assert.Equal(expiredByJob ? 0 : 1, (await ReadQuotaAsync(services, day)).HeldCount);
            await AssertPaidUnallocatedAsync(services, day, attempt, expiredByJob ? "ReservationExpired" : "ReservationExpiredBeforePayment");

            // Job chạy sau đó trả suất nhưng giữ trạng thái PaidUnallocated.
            await using (var scope = services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<HoldService>().ExpireDueAsync(Ct);
            Assert.Equal(0, (await ReadQuotaAsync(services, day)).HeldCount);
            await AssertPaidUnallocatedAsync(services, day, attempt, "Reservation");
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Confirmation_holding_the_lock_wins_over_expiry()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (_, held) = await HeldAttemptAsync(services, day);
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(SECOND, 2, SYSUTCDATETIME()) WHERE Id = '{held.ReservationId}'");
            var confirmed = new TaskCompletionSource<ConfirmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var confirm = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async t =>
                {
                    confirmed.SetResult(await scope.ServiceProvider.GetRequiredService<IConfirmationStore>()
                        .ConfirmAsync(held.ReservationId!.Value, ExamFee, t));
                    await release.Task; // giữ khóa quota + reservation
                }, Ct);
            }, Ct);
            Assert.IsType<ConfirmResult.Confirmed>(await confirmed.Task);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct); // giữ chỗ quá hạn trong lúc xác nhận chưa commit

            var expire = Task.Run(() => ExpireAsync(services, held.ReservationId!.Value), Ct);
            Assert.NotSame(expire, await Task.WhenAny(expire, Task.Delay(BlockedWindow, Ct)));
            release.SetResult();
            await confirm;

            Assert.False(await expire);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 1), (quota.HeldCount, quota.AdmittedCount));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Expiry_holding_the_lock_wins_over_late_confirmation()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (attempt, held) = await HeldAttemptAsync(services, day);
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = '{held.ReservationId}'");
            var expired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var expire = Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async t =>
                {
                    expired.SetResult(await scope.ServiceProvider.GetRequiredService<IHoldStore>().ExpireAsync(held.ReservationId!.Value, t));
                    await release.Task;
                }, Ct);
            }, Ct);
            Assert.True(await expired.Task);

            var handle = Task.Run(() => HandleAsync(services, Paid(attempt)), Ct);
            Assert.NotSame(handle, await Task.WhenAny(handle, Task.Delay(BlockedWindow, Ct)));
            release.SetResult();
            await expire;
            await handle;

            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 0), (quota.HeldCount, quota.AdmittedCount));
            await AssertPaidUnallocatedAsync(services, day, attempt, "ReservationExpired");
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Cls_payment_is_ignored_by_this_handler()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var (attempt, _) = await HeldAttemptAsync(services, day);

            await HandleAsync(services, Paid(attempt) with { Purpose = PaymentPurpose.CLSOrderBatch, EncounterId = Guid.NewGuid() });

            Assert.Equal((1, 0), ((await ReadQuotaAsync(services, day)).HeldCount, (await ReadQuotaAsync(services, day)).AdmittedCount));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.ProcessedEvent WHERE HandlerName = '{PaymentVerifiedHandler.HandlerName}'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    // ---------- helpers W2-06 ----------

    private static PaymentVerified Paid(Guid attempt) =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), PaymentPurpose.InitialConsultation, Guid.NewGuid(), attempt, null, [Guid.NewGuid()],
            DateTime.UtcNow, null);

    private static async Task<(Guid Attempt, HoldOutcome Held)> HeldAttemptAsync(ServiceProvider services, DoctorDayQuota day)
    {
        var patient = Guid.NewGuid();
        var attempt = await SeedAttemptAsync(services, patient);
        var held = await HoldAsync(services, day, attempt, patient);
        Assert.Equal(HoldResult.Held, held.Result);
        return (attempt, held);
    }

    private static async Task HandleAsync(ServiceProvider services, PaymentVerified e)
    {
        await using var scope = services.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<PaymentVerifiedHandler>(scope.ServiceProvider).HandleAsync(e, Ct);
    }

    private static async Task<bool> ExpireAsync(ServiceProvider services, Guid reservationId)
    {
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IHoldStore>();
        return await Uow(scope).ExecuteAsync(t => store.ExpireAsync(reservationId, t), Ct);
    }

    private static async Task AssertPaidUnallocatedAsync(ServiceProvider services, DoctorDayQuota day, Guid attempt, string reasonPrefix)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(BookingAttemptStatus.PaidUnallocated, await db.Set<BookingAttempt>().Where(x => x.Id == attempt).Select(x => x.Status).SingleAsync(Ct));
        Assert.Equal(0, await db.Set<Appointment>().CountAsync(x => x.SessionQuotaId == day.Id, Ct));
        Assert.Equal(0, (await ReadQuotaAsync(services, day)).AdmittedCount);
        var reasons = await CountAsync($"""
            SELECT COUNT(*) FROM workflow.Operation WHERE BookingAttemptId = '{attempt}' AND OperationType = 'ConfirmBooking'
              AND Status = 'ManualReview' AND FailureReason LIKE 'PaidUnallocated: {reasonPrefix}%'
            """);
        Assert.Equal(1, reasons);
    }

    private sealed class FixedFeeBilling : IBillingModule
    {
        public Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct) =>
            Task.FromResult(new FeeQuote(Guid.NewGuid(), "TEST", ChargeType.ConsultationFee, PriceBasis.DoctorRank, null,
                query.DoctorRankId == ExpensiveRank ? 2 * ExamFee : ExamFee, "VND"));

        public Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> CreatePaymentAsync(CreatePayment command, CancellationToken ct) => throw new NotSupportedException();
    }
}
