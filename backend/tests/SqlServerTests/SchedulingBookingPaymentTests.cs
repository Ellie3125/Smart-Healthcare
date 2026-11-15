using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.Appointments;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

// Tạo thanh toán sau giữ chỗ với Billing thật (BillingDbContext tham gia unit of work như ở SmartHealthcare.Api).
public sealed partial class SchedulingHoldTests
{
    private const decimal PaymentExamFee = 275_000m;

    [Fact]
    public async Task Payment_for_a_held_slot_is_created_once_and_moves_the_attempt_to_waiting_payment()
    {
        await using var services = BuildPaymentServices();
        var booking = await HoldForPaymentAsync(services);
        try
        {
            var first = await StartPaymentAsync(services, booking);
            var second = await StartPaymentAsync(services, booking);

            Assert.Equal(BookingPaymentResult.Started, first.Result);
            Assert.Equal((PaymentExamFee, "VND"), (first.Amount, first.Currency));
            Assert.Equal(booking.ExpiresAtUtc, first.ExpiresAtUtc);
            Assert.Equal(first, second);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{booking.AttemptId}'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - {booking.DoctorName}'"));
            Assert.Equal(1, await CountAsync(
                $"SELECT COUNT(*) FROM scheduling.BookingAttempt WHERE Id = '{booking.AttemptId}' AND Status = 'WaitingPayment' AND CurrentStep = 'PaymentInitiated'"));
        }
        finally
        {
            await CleanupPaymentAsync(booking);
        }
    }

    [Fact]
    public async Task Payment_is_refused_after_the_hold_expires_and_writes_nothing()
    {
        await using var services = BuildPaymentServices();
        var booking = await HoldForPaymentAsync(services);
        try
        {
            await ExecAsync($"UPDATE scheduling.Reservation SET ExpiresAtUtc = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE Id = '{booking.ReservationId}'");

            var outcome = await StartPaymentAsync(services, booking);
            var stranger = await StartPaymentAsync(services, booking with { AppUserId = Guid.NewGuid() });

            Assert.Equal(BookingPaymentResult.HoldExpired, outcome.Result);
            Assert.Equal(BookingPaymentResult.AttemptNotFound, stranger.Result);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{booking.AttemptId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - {booking.DoctorName}'"));
        }
        finally
        {
            await CleanupPaymentAsync(booking);
        }
    }

    [Fact]
    public async Task Payment_rolls_back_billing_rows_together_with_the_attempt()
    {
        await using var services = BuildPaymentServices();
        var booking = await HoldForPaymentAsync(services);
        try
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var payments = scope.ServiceProvider.GetRequiredService<BookingPaymentService>();
                await Assert.ThrowsAsync<RollbackSignal>(() => Uow(scope).ExecuteAsync(async t =>
                {
                    var outcome = await payments.StartAsync(booking.AttemptId, booking.AppUserId, (_, _) => Task.FromResult<string?>(booking.DoctorName), t);
                    Assert.Equal(BookingPaymentResult.Started, outcome.Result);
                    throw new RollbackSignal();
                }, Ct));
            }

            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{booking.AttemptId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - {booking.DoctorName}'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.BookingAttempt WHERE Id = '{booking.AttemptId}' AND Status = 'Held'"));
            Assert.Equal(BookingPaymentResult.Started, (await StartPaymentAsync(services, booking)).Result);
        }
        finally
        {
            await CleanupPaymentAsync(booking);
        }
    }

    [Fact]
    public async Task Appointment_reader_returns_only_appointments_booked_by_the_caller()
    {
        await using var services = BuildPaymentServices();
        var booking = await HoldForPaymentAsync(services);
        try
        {
            var appointmentId = Guid.CreateVersion7();
            await ExecAsync($"""
                INSERT INTO scheduling.Appointment (Id, ReservationId, SessionQuotaId, PatientId, AppointmentNumber, AppliedConsultationFee, Status, ConfirmedAtUtc)
                VALUES ('{appointmentId}', '{booking.ReservationId}', '{booking.Day.Id}', '{booking.PatientId}', 'T-{appointmentId.ToString("N")[..12]}', {PaymentExamFee}, 'Scheduled', SYSUTCDATETIME())
                """);

            await using var scope = services.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IPatientAppointmentReader>();
            var mine = Assert.Single(await reader.ListAsync(booking.AppUserId, 0, 10, Ct));
            var detail = await reader.GetAsync(appointmentId, booking.AppUserId, Ct);

            Assert.Equal(mine, detail);
            Assert.Equal((appointmentId, AppointmentStatus.Scheduled, booking.PatientId, booking.Day.DoctorId, ServiceDate, booking.Day.AssignedRoomId, PaymentExamFee),
                (mine.AppointmentId, mine.Status, mine.PatientId, mine.DoctorId, mine.ServiceDate, mine.AssignedRoomId, mine.AppliedConsultationFee));
            Assert.Empty(await reader.ListAsync(Guid.NewGuid(), 0, 10, Ct));
            Assert.Null(await reader.GetAsync(appointmentId, Guid.NewGuid(), Ct));
            Assert.Empty(await reader.ListAsync(booking.AppUserId, 1, 10, Ct));
        }
        finally
        {
            await CleanupPaymentAsync(booking);
        }
    }

    private sealed record HeldForPayment(DoctorDayQuota Day, Guid AttemptId, Guid AppUserId, Guid PatientId, Guid ReservationId,
        DateTime ExpiresAtUtc, string DoctorName, string PriceListCode);

    private static ServiceProvider BuildPaymentServices()
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection()
            .AddSharedInfrastructure(cs)
            .AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs)
            .AddBillingInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddUnitOfWorkParticipant<BillingDbContext>()
            .AddSchedulingApplication()
            .BuildServiceProvider(validateScopes: true);
    }

    private static async Task<HeldForPayment> HoldForPaymentAsync(ServiceProvider services)
    {
        var day = await SeedDayAsync(services);
        var patient = Guid.NewGuid();
        var attempt = await SeedAttemptAsync(services, patient);
        var rank = Guid.NewGuid();
        var code = "T-" + Guid.NewGuid().ToString("N")[..10];
        await ExecAsync($"""
            DECLARE @list uniqueidentifier = NEWID();
            INSERT INTO billing.PriceList (Id, PriceListCode, PriceListName, Currency, EffectiveFromUtc, IsActive)
            VALUES (@list, '{code}', N'test', 'VND', DATEADD(DAY, -1, SYSUTCDATETIME()), 1);
            INSERT INTO billing.PriceListItem (Id, PriceListId, ItemType, DoctorRankId, UnitPrice, IsActive)
            VALUES (NEWID(), @list, 'ConsultationFee', '{rank}', {PaymentExamFee}, 1);
            """);

        var request = Request(day, attempt, patient) with { DoctorRankId = rank };
        await ExecAsync($"UPDATE scheduling.BookingAttempt SET AppUserId = '{request.AppUserId}' WHERE Id = '{attempt}'");
        var held = await HoldAsync(services, request);
        Assert.Equal(HoldResult.Held, held.Result);
        return new HeldForPayment(day, attempt, request.AppUserId, patient, held.ReservationId!.Value, held.ExpiresAtUtc!.Value,
            "TEST-" + Guid.NewGuid().ToString("N")[..12], code);
    }

    private static async Task<BookingPaymentOutcome> StartPaymentAsync(ServiceProvider services, HeldForPayment booking)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BookingPaymentService>()
            .StartAsync(booking.AttemptId, booking.AppUserId, (_, _) => Task.FromResult<string?>(booking.DoctorName), Ct);
    }

    private static async Task CleanupPaymentAsync(HeldForPayment booking)
    {
        await ExecAsync($"""
            DELETE FROM billing.PaymentAllocation WHERE PaymentTransactionId IN (SELECT t.Id FROM billing.PaymentTransaction t
                JOIN billing.PaymentIntent i ON i.Id = t.PaymentIntentId WHERE i.BookingAttemptId = '{booking.AttemptId}');
            DELETE FROM billing.PaymentTransaction WHERE PaymentIntentId IN (SELECT Id FROM billing.PaymentIntent WHERE BookingAttemptId = '{booking.AttemptId}');
            DELETE FROM billing.PaymentIntent WHERE BookingAttemptId = '{booking.AttemptId}';
            DELETE FROM billing.BillingItem WHERE ItemNameSnapshot = N'Phí khám - {booking.DoctorName}';
            DELETE FROM billing.PriceListItem WHERE PriceListId IN (SELECT Id FROM billing.PriceList WHERE PriceListCode = '{booking.PriceListCode}');
            DELETE FROM billing.PriceList WHERE PriceListCode = '{booking.PriceListCode}';
            DELETE FROM shared.ApiIdempotencyRecord WHERE CallerScope = 'AppUser:{booking.AppUserId:D}';
            """);
        await CleanupAsync(booking.Day);
    }
}
