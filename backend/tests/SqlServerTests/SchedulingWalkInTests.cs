using System.Reflection;
using System.Text.Json;
using Billing.Contracts;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Holds;
using Scheduling.Application.WalkIn;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

public sealed partial class SchedulingHoldTests
{
    [Fact]
    public async Task WalkIn_unknown_patient_is_refused_before_creating_attempt_or_collecting_cash()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = WalkInServices(clock, patientExists: false);
        var day = await SeedBoundaryDayAsync(services, clock);
        var actor = Guid.NewGuid();
        var price = await SeedWalkInPriceAsync(day);
        try
        {
            var outcome = await RegisterWalkInAsync(services, new(Guid.NewGuid(), day.DoctorId, actor, Key()));
            Assert.Equal(WalkInResult.PatientNotFound, outcome.Result);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.BookingAttempt WHERE AppUserId = '{actor}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE PayerAppUserId = '{actor}'"));
        }
        finally { await CleanupWalkInAsync(day, actor, price); }
    }

    [Theory]
    [InlineData(30, 8, 0, HoldResult.Full)]
    [InlineData(0, 9, 30, HoldResult.CutoffPassed)]
    public async Task WalkIn_rejected_hold_never_creates_a_charge_or_cash_receipt(int admitted, int hour, int minute, HoldResult expected)
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = WalkInServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var actor = Guid.NewGuid();
        var price = await SeedWalkInPriceAsync(day);
        try
        {
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET AdmittedCount = {admitted} WHERE Id = '{day.Id}'");
            clock.NowUtc = AtIct(hour, minute);
            var outcome = await RegisterWalkInAsync(services, new(Guid.NewGuid(), day.DoctorId, actor, Key()));
            Assert.Equal(WalkInResult.HoldRejected, outcome.Result);
            Assert.Equal(expected, outcome.HoldFailure);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE PayerAppUserId = '{actor}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - Walkin-{day.DoctorId:N}'"));
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, admitted), (quota.HeldCount, quota.AdmittedCount));
        }
        finally { await CleanupWalkInAsync(day, actor, price); }
    }

    [Fact]
    public async Task WalkIn_check_in_failure_rolls_back_cash_charge_encounter_and_capacity()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = WalkInServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var actor = Guid.NewGuid();
        var patient = Guid.NewGuid();
        var price = await SeedWalkInPriceAsync(day);
        try
        {
            var request = new WalkInRequest(patient, day.DoctorId, actor, Key());
            await using (var scope = services.CreateAsyncScope())
            {
                var checkIn = ActivatorUtilities.CreateInstance<CheckInService>(scope.ServiceProvider, new FailingCheckInPublisher());
                var service = ActivatorUtilities.CreateInstance<WalkInService>(scope.ServiceProvider, checkIn);
                await Assert.ThrowsAsync<RollbackSignal>(() => service.RegisterAsync(request, Ct));
            }
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE PayerAppUserId = '{actor}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - Walkin-{day.DoctorId:N}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.BookingAttempt WHERE AppUserId = '{actor}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM clinical.Encounter WHERE PatientId = '{patient}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TicketNumber LIKE '{day.Id:N}-%'"));
            Assert.Equal((0, 0), ((await ReadQuotaAsync(services, day)).HeldCount, (await ReadQuotaAsync(services, day)).AdmittedCount));
            Assert.Equal(WalkInResult.Registered, (await RegisterWalkInAsync(services, request)).Result);
        }
        finally { await CleanupWalkInAsync(day, actor, price); }
    }

    [Fact]
    public async Task WalkIn_collects_cash_confirms_and_checks_in_immediately_and_replays_once()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = WalkInServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var actor = Guid.NewGuid();
        var price = await SeedWalkInPriceAsync(day);
        try
        {
            var request = new WalkInRequest(Guid.NewGuid(), day.DoctorId, actor, Key());
            ((WalkInIdentity)services.GetRequiredService<IIdentityPatientsModule>()).DisplayRoom = day.AssignedRoomId;
            var concurrent = await Task.WhenAll(RegisterWalkInAsync(services, request), RegisterWalkInAsync(services, request));
            var result = concurrent[0];
            Assert.Equal(result, concurrent[1]);
            Assert.Equal(WalkInResult.Registered, result.Result);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.Equal("Registered", json.RootElement.GetProperty("result").GetString());
            Assert.Equal((PaymentExamFee, "VND", false), (result.Receipt!.Amount, result.Receipt.Currency, result.Receipt.AlreadyCollected));
            Assert.Equal(1, result.CheckIn!.SequenceNumber);
            Assert.False(result.CheckIn.AlreadyCheckedIn);
            Assert.Equal("P101-001", result.CheckIn.DisplayNumber);
            Assert.Equal(result, await RegisterWalkInAsync(services, request));
            Assert.Equal(WalkInResult.IdempotencyConflict, (await RegisterWalkInAsync(services, request with { PatientId = Guid.NewGuid() })).Result);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{result.BookingAttemptId}' AND Provider = 'Counter' AND Status = 'Succeeded'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Appointment WHERE Id = '{result.CheckIn.AppointmentId}' AND Status = 'CheckedIn'"));
            Assert.Equal((0, 1), ((await ReadQuotaAsync(services, day)).HeldCount, (await ReadQuotaAsync(services, day)).AdmittedCount));
            // Worker nhận lại đúng event đã xử lý đồng bộ: không tạo DuplicatePayment refund.
            await using var scope = services.CreateAsyncScope();
            var e = await Uow(scope).ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<OutboxReader>()
                .ReadSingleAsync<PaymentVerified>(result.Receipt.PaymentIntentId, t), Ct);
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<PaymentVerified>>().HandleAsync(e, Ct);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.RefundRequest WHERE OriginalPaymentTransactionId IN (SELECT Id FROM billing.PaymentTransaction WHERE PaymentIntentId = '{result.Receipt.PaymentIntentId}')"));
        }
        finally { await CleanupWalkInAsync(day, actor, price); }
    }

    [Fact]
    public async Task WalkIn_and_online_contend_for_the_thirtieth_slot_only_one_is_admitted()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = WalkInServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var actor = Guid.NewGuid();
        var price = await SeedWalkInPriceAsync(day);
        try
        {
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET AdmittedCount = 29 WHERE Id = '{day.Id}'");
            var patient = Guid.NewGuid();
            var attempt = await SeedAttemptAsync(services, patient);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var online = Task.Run(async () => { await start.Task.WaitAsync(Ct); return await HoldAsync(services, day, attempt, patient); }, Ct);
            var counter = Task.Run(async () => { await start.Task.WaitAsync(Ct); return await RegisterWalkInAsync(services, new(Guid.NewGuid(), day.DoctorId, actor, Key())); }, Ct);
            start.SetResult();
            await Task.WhenAll(online, counter);
            var onlineWon = (await online).Result == HoldResult.Held;
            var counterWon = (await counter).Result == WalkInResult.Registered;
            Assert.NotEqual(onlineWon, counterWon);
            if (onlineWon)
            {
                Assert.Equal(HoldResult.Full, (await counter).HoldFailure);
                await HandleAsync(services, Paid(attempt));
            }
            else
                Assert.Equal(HoldResult.Full, (await online).Result);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 30), (quota.HeldCount, quota.AdmittedCount));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Appointment WHERE SessionQuotaId = '{day.Id}'"));
            Assert.Equal(counterWon ? 1 : 0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE PayerAppUserId = '{actor}'"));
        }
        finally { await CleanupWalkInAsync(day, actor, price); }
    }

    private static ServiceProvider WalkInServices(BoundaryDbClock clock, bool patientExists = true)
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs).AddBillingInfrastructure(cs).AddClinicalInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddUnitOfWorkParticipant<BillingDbContext>().AddUnitOfWorkParticipant<ClinicalDbContext>()
            .AddSchedulingApplication().AddSingleton<IDbClock>(clock)
            .AddSingleton<TimeProvider>(new WalkInTime(clock))
            .AddSingleton(DispatchProxy.Create<IIdentityPatientsModule, WalkInIdentity>())
            .AddSingleton<IWalkInPatientDirectory>(new KnownWalkInPatient(patientExists))
            .BuildServiceProvider(validateScopes: true);
    }

    private sealed class WalkInTime(BoundaryDbClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(clock.NowUtc);
    }
    private sealed class KnownWalkInPatient(bool exists) : IWalkInPatientDirectory
    {
        public Task<bool> ExistsAsync(Guid patientId, CancellationToken ct) => Task.FromResult(exists && patientId != Guid.Empty);
    }
    public class WalkInIdentity : DispatchProxy
    {
        public Guid DisplayRoom { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "GetRoomsAsync"
            ? Task.FromResult<IReadOnlyList<RoomDto>>([new(DisplayRoom, "P101", "Test", "Clinical", true)])
            : method?.Name == "GetDoctorAsync"
            ? Task.FromResult<DoctorDto?>(new((Guid)args![0]!, $"Walkin-{(Guid)args[0]!:N}", Guid.NewGuid(), "TEST", Guid.NewGuid(), "BS", true))
            : throw new NotSupportedException(method?.Name);
    }

    private static async Task<string> SeedWalkInPriceAsync(DoctorDayQuota day)
    {
        var code = "WI-" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync($"""
            DECLARE @list uniqueidentifier = NEWID();
            INSERT INTO billing.PriceList (Id, PriceListCode, PriceListName, Currency, EffectiveFromUtc, IsActive)
            VALUES (@list, '{code}', N'Walk-in test', 'VND', '2098-01-01', 1);
            INSERT INTO billing.PriceListItem (Id, PriceListId, ItemType, DoctorId, UnitPrice, IsActive)
            VALUES (NEWID(), @list, 'ConsultationFee', '{day.DoctorId}', {PaymentExamFee}, 1);
            """);
        return code;
    }

    private static async Task<WalkInOutcome> RegisterWalkInAsync(ServiceProvider services, WalkInRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WalkInService>().RegisterAsync(request, Ct);
    }

    private static async Task CleanupWalkInAsync(DoctorDayQuota day, Guid actor, string price)
    {
        await ExecAsync($"""
            DECLARE @attempts TABLE (Id uniqueidentifier);
            INSERT INTO @attempts SELECT Id FROM scheduling.BookingAttempt WHERE AppUserId = '{actor}'
                OR Id IN (SELECT BookingAttemptId FROM scheduling.Reservation WHERE SessionQuotaId = '{day.Id}');
            DECLARE @intents TABLE (Id uniqueidentifier);
            INSERT INTO @intents SELECT Id FROM billing.PaymentIntent WHERE BookingAttemptId IN (SELECT Id FROM @attempts);
            DELETE FROM shared.OutboxMessage WHERE AggregateId IN (SELECT Id FROM @intents);
            DELETE FROM shared.AuditLog WHERE PerformedByUserId = '{actor}';
            DELETE FROM billing.PaymentEvent WHERE PaymentTransactionId IN (SELECT Id FROM billing.PaymentTransaction WHERE PaymentIntentId IN (SELECT Id FROM @intents));
            DELETE FROM billing.PaymentAllocation WHERE PaymentTransactionId IN (SELECT Id FROM billing.PaymentTransaction WHERE PaymentIntentId IN (SELECT Id FROM @intents));
            DELETE FROM billing.PaymentTransaction WHERE PaymentIntentId IN (SELECT Id FROM @intents);
            DELETE FROM billing.PaymentIntent WHERE Id IN (SELECT Id FROM @intents);
            DELETE FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - Walkin-{day.DoctorId:N}';
            DELETE FROM billing.PriceListItem WHERE PriceListId IN (SELECT Id FROM billing.PriceList WHERE PriceListCode = '{price}');
            DELETE FROM billing.PriceList WHERE PriceListCode = '{price}';
            DELETE FROM shared.ApiIdempotencyRecord WHERE CallerScope = 'AppUser:{actor:D}';
            """);
        await CleanupAttendanceAsync(day);
    }
}
