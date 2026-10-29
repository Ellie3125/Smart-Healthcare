using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Billing.Infrastructure;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Catalog.Infrastructure;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Scheduling.Infrastructure;
using CatalogCurrency = SmartHealthcare.Catalog.Domain.Currency;
using BillingCurrency = SmartHealthcare.Billing.Domain.Currency;
using BillingItemType = SmartHealthcare.Billing.Domain.ItemType;
using CatalogItemType = SmartHealthcare.Catalog.Domain.ItemType;

namespace SmartHealthcare.Database.Tests;

/// <summary>Ràng buộc chạy trên SQL Server thật, gồm 4 quyết định đã chốt (1B, 2A, 3B, 4B).</summary>
public sealed class SqlServerConstraintTests(SqlServerFixture db) : IClassFixture<SqlServerFixture>
{
    private const int CheckOrFkViolation = 547;
    private const int UniqueIndexViolation = 2601;
    private static readonly DateTime Now = new(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Day = new(2026, 11, 2);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invalid_enum_values_are_rejected()
    {
        Skip();
        var (appointment, _) = await SeedAppointmentAsync();
        await Rejected(CheckOrFkViolation, $"UPDATE [scheduling].[Appointment] SET [Status] = N'Bogus' WHERE [Id] = '{appointment.Id}'");
        await Rejected(CheckOrFkViolation, "INSERT INTO [clinical].[Encounter] ([Id],[AppointmentId],[PatientId],[OriginalDoctorId],[OriginalRoomId],[SpecialtySnapshot],[DoctorRankSnapshot],[CheckedInAtUtc],[Status]) " +
            $"VALUES (NEWID(), NEWID(), NEWID(), NEWID(), NEWID(), N'S', N'R', '{Now:O}', N'Closed')");
    }

    [Fact]
    public async Task Appointment_and_encounter_accept_new_statuses_1B_2A()
    {
        Skip();
        var (appointment, _) = await SeedAppointmentAsync();
        await Exec($"UPDATE [scheduling].[Appointment] SET [Status] = N'Rescheduled' WHERE [Id] = '{appointment.Id}'");
        await Exec("INSERT INTO [clinical].[Encounter] ([Id],[AppointmentId],[PatientId],[OriginalDoctorId],[OriginalRoomId],[SpecialtySnapshot],[DoctorRankSnapshot],[CheckedInAtUtc],[Status]) " +
            $"VALUES (NEWID(), NEWID(), NEWID(), NEWID(), NEWID(), N'S', N'R', '{Now:O}', N'ReturningDoctor')");
    }

    [Fact]
    public async Task Appointment_can_be_rescheduled_only_once_1B()
    {
        Skip();
        var (original, quota) = await SeedAppointmentAsync();
        await using var context = db.Context<SchedulingDbContext>("scheduling");
        context.Add(NewAppointment(quota.Id, original.Id));
        await context.SaveChangesAsync(Ct);

        context.Add(NewAppointment(quota.Id, original.Id));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        Assert.Equal(UniqueIndexViolation, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task Payment_providers_follow_3B()
    {
        Skip();
        var intent = await SeedPaymentAsync();
        await Exec($"UPDATE [billing].[PaymentIntent] SET [Provider] = N'Counter' WHERE [Id] = '{intent.Id}'");
        await Rejected(CheckOrFkViolation, $"UPDATE [billing].[PaymentIntent] SET [Provider] = N'Cash' WHERE [Id] = '{intent.Id}'");
        await Rejected(CheckOrFkViolation, "INSERT INTO [billing].[PaymentEvent] ([Id],[Provider],[EventId],[EventType],[PayloadHash],[PayloadJson],[ReceivedAtUtc],[Status]) " +
            $"VALUES (NEWID(), N'Counter', N'e1', N'PaymentSuccess', N'h', N'{{}}', '{Now:O}', N'Received')");
    }

    [Fact]
    public async Task Refund_reason_must_match_policy_4B()
    {
        Skip();
        var intent = await SeedPaymentAsync();
        var refund = await SeedRefundAsync(intent);
        await Rejected(CheckOrFkViolation, $"UPDATE [billing].[RefundRequest] SET [PolicyRuleApplied] = N'CustomApproval' WHERE [Id] = '{refund.Id}'");
        await Rejected(CheckOrFkViolation, $"UPDATE [billing].[RefundRequest] SET [RefundReason] = N'DuplicatePayment' WHERE [Id] = '{refund.Id}'");
        await Exec($"UPDATE [billing].[RefundRequest] SET [RefundReason] = N'PaidUnallocated', [PolicyRuleApplied] = N'CustomApproval' WHERE [Id] = '{refund.Id}'");
    }

    [Fact]
    public async Task Quota_counts_cannot_exceed_capacity()
    {
        Skip();
        var (_, quota) = await SeedAppointmentAsync();
        await Rejected(CheckOrFkViolation, $"UPDATE [scheduling].[DoctorSessionSlotQuota] SET [HeldCount] = 20, [AdmittedCount] = 11 WHERE [Id] = '{quota.Id}'");
        await Rejected(CheckOrFkViolation, $"UPDATE [scheduling].[DoctorSessionSlotQuota] SET [HeldCount] = -1 WHERE [Id] = '{quota.Id}'");
        await Exec($"UPDATE [scheduling].[DoctorSessionSlotQuota] SET [HeldCount] = 10, [AdmittedCount] = 10, [LockedCancelledCount] = 10 WHERE [Id] = '{quota.Id}'");
    }

    [Fact]
    public async Task Active_booking_claim_is_unique_only_while_active()
    {
        Skip();
        var (appointment, quota) = await SeedAppointmentAsync();
        string Insert(int active) =>
            "INSERT INTO [scheduling].[ActiveBookingClaim] ([Id],[SessionQuotaId],[DoctorId],[ServiceDate],[PatientId],[ReservationId],[ClaimSource],[IsActive],[CreatedAtUtc]) " +
            $"VALUES (NEWID(), '{quota.Id}', '{quota.DoctorId}', '{Day:yyyy-MM-dd}', '{appointment.PatientId}', '{appointment.ReservationId}', N'App', {active}, '{Now:O}')";
        await Exec(Insert(0));
        await Exec(Insert(0));
        await Exec(Insert(1));
        await Rejected(UniqueIndexViolation, Insert(1));
    }

    [Fact]
    public async Task Price_list_item_targets_match_item_type()
    {
        Skip();
        await using var context = db.Context<CatalogDbContext>("catalog");
        var rank = new DoctorRank { Id = Guid.NewGuid(), RankCode = RankCode.Master, RankName = "ThS", DisplayOrder = 1, IsActive = true };
        var list = new PriceList { Id = Guid.NewGuid(), PriceListCode = Code(), PriceListName = "2026", Currency = CatalogCurrency.VND, EffectiveFromUtc = Now, IsActive = true };
        context.AddRange(rank, list);
        await context.SaveChangesAsync(Ct);

        string Insert(string type, string? rankId, string? doctorId, string? serviceId, decimal price) =>
            "INSERT INTO [catalog].[PriceListItem] ([Id],[PriceListId],[ItemType],[DoctorRankId],[DoctorId],[MedicalServiceId],[UnitPrice],[IsActive]) " +
            $"VALUES (NEWID(), '{list.Id}', N'{type}', {Sql(rankId)}, {Sql(doctorId)}, {Sql(serviceId)}, {price}, 1)";
        var doctor = Guid.NewGuid().ToString();
        await Exec(Insert(nameof(CatalogItemType.ConsultationFee), rank.Id.ToString(), doctor, null, 150000));
        await Rejected(CheckOrFkViolation, Insert(nameof(CatalogItemType.ConsultationFee), null, null, null, 150000));
        await Rejected(CheckOrFkViolation, Insert(nameof(CatalogItemType.CLSService), null, doctor, null, 150000));
        await Rejected(CheckOrFkViolation, Insert(nameof(CatalogItemType.ConsultationFee), rank.Id.ToString(), null, null, -1));
    }

    [Fact]
    public async Task Billing_amounts_are_checked()
    {
        Skip();
        var intent = await SeedPaymentAsync();
        await using var context = db.Context<BillingDbContext>("billing");
        var item = await context.Set<BillingItem>().FirstAsync(b => b.Id == _billingItemId, Ct);
        var transaction = await context.Set<PaymentTransaction>().FirstAsync(t => t.PaymentIntentId == intent.Id, Ct);
        await Rejected(CheckOrFkViolation, $"UPDATE [billing].[BillingItem] SET [TotalAmount] = 1 WHERE [Id] = '{item.Id}'");
        await Rejected(CheckOrFkViolation, "INSERT INTO [billing].[PaymentAllocation] ([Id],[PaymentTransactionId],[BillingItemId],[AllocatedAmount],[AllocatedAtUtc]) " +
            $"VALUES (NEWID(), '{transaction.Id}', '{item.Id}', 0, '{Now:O}')");
    }

    [Fact]
    public async Task Datetimes_round_trip_as_utc_and_rowversion_detects_conflicts()
    {
        Skip();
        var (appointment, _) = await SeedAppointmentAsync();
        await using var first = db.Context<SchedulingDbContext>("scheduling");
        await using var second = db.Context<SchedulingDbContext>("scheduling");
        var a = await first.Set<Appointment>().SingleAsync(x => x.Id == appointment.Id, Ct);
        var b = await second.Set<Appointment>().SingleAsync(x => x.Id == appointment.Id, Ct);
        Assert.Equal(DateTimeKind.Utc, a.ConfirmedAtUtc.Kind);

        a.CancellationReason = "first";
        await first.SaveChangesAsync(Ct);
        b.CancellationReason = "second";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    private Guid _billingItemId;

    private void Skip() => Assert.SkipUnless(db.Available, "Chưa đặt SH_TEST_SQL_CONNECTION");

    private async Task<(Appointment Appointment, DoctorSessionSlotQuota Quota)> SeedAppointmentAsync()
    {
        await using var context = db.Context<SchedulingDbContext>("scheduling");
        var quota = new DoctorSessionSlotQuota
        {
            Id = Guid.NewGuid(), DoctorId = Guid.NewGuid(), AssignedRoomId = Guid.NewGuid(), ServiceDate = Day, Capacity = 30,
            SessionStartTimeUtc = Now, BookingCutoffTimeUtc = Now, RescheduleCutoffTimeUtc = Now,
            SessionStatus = SessionStatus.Scheduled, IsOpen = true,
        };
        var appointment = NewAppointment(quota.Id, null);
        appointment.ConfirmedAtUtc = new DateTime(2026, 10, 29, 7, 0, 0, DateTimeKind.Unspecified);
        context.AddRange(quota, appointment);
        await context.SaveChangesAsync(Ct);
        return (appointment, quota);
    }

    private static Appointment NewAppointment(Guid quotaId, Guid? rescheduledFrom)
    {
        var patient = Guid.NewGuid();
        return new Appointment
        {
            Id = Guid.NewGuid(), SessionQuotaId = quotaId, PatientId = patient, RescheduledFromAppointmentId = rescheduledFrom,
            AppointmentNumber = Code(), AppliedConsultationFee = 150000, Status = AppointmentStatus.Scheduled, ConfirmedAtUtc = Now,
            Reservation = new Reservation
            {
                Id = Guid.NewGuid(), SessionQuotaId = quotaId, BookingAttemptId = Guid.NewGuid(), PatientId = patient,
                AppUserId = Guid.NewGuid(), BookingChannel = BookingChannel.Web, ReservedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(5),
                Status = ReservationStatus.Confirmed, IdempotencyKey = Code(),
            },
        };
    }

    private async Task<PaymentIntent> SeedPaymentAsync()
    {
        await using var context = db.Context<BillingDbContext>("billing");
        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(), IntentNumber = Code(), PayerAppUserId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            TotalExpectedAmount = 150000, Currency = BillingCurrency.VND, Provider = PaymentIntentProvider.MoMo,
            PaymentType = PaymentType.InitialConsultation, Status = PaymentIntentStatus.Succeeded, CreatedAtUtc = Now, ExpiresAtUtc = Now,
        };
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(), PaymentIntent = intent, TransactionReference = Code(), TransactionAmount = 150000,
            Currency = BillingCurrency.VND, PaymentStatus = PaymentStatus.Success,
        };
        var item = new BillingItem
        {
            Id = Guid.NewGuid(), PriceListItemId = Guid.NewGuid(), ItemType = BillingItemType.ConsultationFee, ItemNameSnapshot = "Phí khám",
            UnitPrice = 150000, Quantity = 1, TotalAmount = 150000, PaidAmount = 150000, Status = BillingItemStatus.Paid, CreatedAtUtc = Now,
        };
        context.AddRange(intent, transaction, item);
        await context.SaveChangesAsync(Ct);
        _billingItemId = item.Id;
        return intent;
    }

    private async Task<RefundRequest> SeedRefundAsync(PaymentIntent intent)
    {
        await using var context = db.Context<BillingDbContext>("billing");
        var transaction = await context.Set<PaymentTransaction>().FirstAsync(t => t.PaymentIntentId == intent.Id, Ct);
        var refund = new RefundRequest
        {
            Id = Guid.NewGuid(), BillingItemId = _billingItemId, OriginalPaymentTransactionId = transaction.Id, RequestedAmount = 150000,
            RefundReason = RefundReason.CancelledBefore0500, PolicyRuleApplied = PolicyRuleApplied.FullRefundPre0500,
            Status = RefundRequestStatus.Pending, RequestedAtUtc = Now,
        };
        context.Add(refund);
        await context.SaveChangesAsync(Ct);
        return refund;
    }

    private async Task Exec(string sql)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task Rejected(int errorNumber, string sql)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => Exec(sql));
        Assert.Equal(errorNumber, error.Number);
    }

    private static string Code() => Guid.NewGuid().ToString("N")[..20];

    private static string Sql(string? id) => id is null ? "NULL" : $"'{id}'";
}
