using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Ràng buộc của ERD v2.4 chạy trên SQL Server thật (DB SmartHealthcare_Test); mỗi test rollback khi kết thúc.
public class ConstraintTests
{
    // ---------- CHECK ----------

    [Fact]
    public async Task DoctorDayQuota_counts_are_non_negative_and_within_capacity()
    {
        await using var db = await OpenAsync();
        await db.InsertAsync("scheduling.DoctorDayQuota", new { Capacity = 2, HeldCount = 1, AdmittedCount = 1, LockedCancelledCount = 0 });

        await db.RejectsAsync(CheckViolation, "scheduling.DoctorDayQuota", new { Capacity = 2, HeldCount = -1 });
        await db.RejectsAsync(CheckViolation, "scheduling.DoctorDayQuota", new { Capacity = 2, HeldCount = 2, AdmittedCount = 0, LockedCancelledCount = 1 });
    }

    [Fact]
    public async Task PriceListItem_targets_follow_item_type_in_both_directions()
    {
        await using var db = await OpenAsync();
        var list = await db.InsertAsync("billing.PriceList");
        object Item(string type, Guid? rank, Guid? doctor, Guid? service, decimal price = 150000) => new
        {
            PriceListId = list, ItemType = type, DoctorRankId = rank, DoctorId = doctor, MedicalServiceId = service, UnitPrice = price,
        };

        await db.InsertAsync("billing.PriceListItem", Item("ConsultationFee", Guid.NewGuid(), null, null));
        await db.InsertAsync("billing.PriceListItem", Item("ConsultationFee", null, Guid.NewGuid(), null));
        await db.InsertAsync("billing.PriceListItem", Item("CLSService", null, null, Guid.NewGuid()));

        await db.RejectsAsync(CheckViolation, "billing.PriceListItem", Item("ConsultationFee", null, null, null));
        await db.RejectsAsync(CheckViolation, "billing.PriceListItem", Item("ConsultationFee", Guid.NewGuid(), null, Guid.NewGuid()));
        await db.RejectsAsync(CheckViolation, "billing.PriceListItem", Item("CLSService", null, Guid.NewGuid(), Guid.NewGuid()));
        await db.RejectsAsync(CheckViolation, "billing.PriceListItem", Item("CLSService", null, null, null));
        await db.RejectsAsync(CheckViolation, "billing.PriceListItem", Item("ConsultationFee", Guid.NewGuid(), null, null, -1));
    }

    [Fact]
    public async Task BillingItem_total_equals_unit_price_times_quantity()
    {
        await using var db = await OpenAsync();
        var priceListItem = await ConsultationPriceListItemAsync(db);

        await db.InsertAsync("billing.BillingItem", new { PriceListItemId = priceListItem, UnitPrice = 150000m, Quantity = 2, TotalAmount = 300000m });
        await db.RejectsAsync(CheckViolation, "billing.BillingItem", new { PriceListItemId = priceListItem, UnitPrice = 150000m, Quantity = 2, TotalAmount = 150000m });
    }

    [Fact]
    public async Task PaymentAllocation_amount_must_be_positive()
    {
        await using var db = await OpenAsync();
        var billingItem = await db.InsertAsync("billing.BillingItem", new { PriceListItemId = await ConsultationPriceListItemAsync(db) });
        var transaction = await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = await db.InsertAsync("billing.PaymentIntent") });

        await db.InsertAsync("billing.PaymentAllocation", new { PaymentTransactionId = transaction, BillingItemId = billingItem, AllocatedAmount = 1m });
        await db.RejectsAsync(CheckViolation, "billing.PaymentAllocation", new { PaymentTransactionId = transaction, BillingItemId = billingItem, AllocatedAmount = 0m });
    }

    [Fact]
    public async Task RefundRequest_policy_rule_matches_refund_reason()
    {
        await using var db = await OpenAsync();
        var billingItem = await db.InsertAsync("billing.BillingItem", new { PriceListItemId = await ConsultationPriceListItemAsync(db) });
        var transaction = await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = await db.InsertAsync("billing.PaymentIntent") });
        object Refund(string reason, string policy) => new
        {
            BillingItemId = billingItem, OriginalPaymentTransactionId = transaction, RefundReason = reason, PolicyRuleApplied = policy,
        };

        await db.InsertAsync("billing.RefundRequest", Refund("CancelledBefore0500", "FullRefundPre0500"));
        await db.InsertAsync("billing.RefundRequest", Refund("PaidUnallocated", "CustomApproval"));

        await db.RejectsAsync(CheckViolation, "billing.RefundRequest", Refund("CancelledBefore0500", "CustomApproval"));
        await db.RejectsAsync(CheckViolation, "billing.RefundRequest", Refund("DuplicatePayment", "FullRefundPre0500"));
    }

    // ---------- Unique có lọc IS NOT NULL / IsActive = 1 ----------

    [Fact]
    public async Task Patient_NationalId_is_unique_when_present()
    {
        await using var db = await OpenAsync();
        await db.InsertAsync("identity.Patient", new { NationalId = (string?)null });
        await db.InsertAsync("identity.Patient", new { NationalId = (string?)null });
        await db.InsertAsync("identity.Patient", new { NationalId = "079200000001" });

        await db.RejectsAsync(UniqueIndexViolation, "identity.Patient", new { NationalId = "079200000001" });
    }

    [Fact]
    public async Task AspNetUsers_email_and_phone_are_unique_when_present()
    {
        await using var db = await OpenAsync();
        await db.InsertAsync("identity.AspNetUsers", new { Email = (string?)null, PhoneNumber = (string?)null });
        await db.InsertAsync("identity.AspNetUsers", new { Email = (string?)null, PhoneNumber = (string?)null });
        await db.InsertAsync("identity.AspNetUsers", new { Email = "a@example.test", PhoneNumber = "0900000001" });

        await db.RejectsAsync(UniqueIndexViolation, "identity.AspNetUsers", new { Email = "a@example.test", PhoneNumber = (string?)null });
        await db.RejectsAsync(UniqueIndexViolation, "identity.AspNetUsers", new { Email = (string?)null, PhoneNumber = "0900000001" });
    }

    [Fact]
    public async Task Reservation_IdempotencyKey_is_unique_when_present()
    {
        await using var db = await OpenAsync();
        var quota = await db.InsertAsync("scheduling.DoctorDayQuota", new { Capacity = 30 });
        var attempt = await db.InsertAsync("scheduling.BookingAttempt");
        object Reservation(string? key) => new { SessionQuotaId = quota, BookingAttemptId = attempt, IdempotencyKey = key };

        await db.InsertAsync("scheduling.Reservation", Reservation(null));
        await db.InsertAsync("scheduling.Reservation", Reservation(null));
        await db.InsertAsync("scheduling.Reservation", Reservation("idem-1"));

        await db.RejectsAsync(UniqueIndexViolation, "scheduling.Reservation", Reservation("idem-1"));
    }

    [Fact]
    public async Task PaymentTransaction_ProviderTransactionId_is_unique_when_present()
    {
        await using var db = await OpenAsync();
        var intent = await db.InsertAsync("billing.PaymentIntent");
        object Transaction(string? providerId) => new { PaymentIntentId = intent, ProviderTransactionId = providerId };

        await db.InsertAsync("billing.PaymentTransaction", Transaction(null));
        await db.InsertAsync("billing.PaymentTransaction", Transaction(null));
        await db.InsertAsync("billing.PaymentTransaction", Transaction("gw-1"));

        await db.RejectsAsync(UniqueIndexViolation, "billing.PaymentTransaction", Transaction("gw-1"));
    }

    [Fact]
    public async Task RefundTransaction_ProviderRefundTransactionId_is_unique_when_present()
    {
        await using var db = await OpenAsync();
        var billingItem = await db.InsertAsync("billing.BillingItem", new { PriceListItemId = await ConsultationPriceListItemAsync(db) });
        var transaction = await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = await db.InsertAsync("billing.PaymentIntent") });
        async Task<object> Refund(string? providerId) => new
        {
            RefundRequestId = await db.InsertAsync("billing.RefundRequest", new
            {
                BillingItemId = billingItem, OriginalPaymentTransactionId = transaction, RefundReason = "PaidUnallocated", PolicyRuleApplied = "CustomApproval",
            }),
            OriginalPaymentTransactionId = transaction,
            ProviderRefundTransactionId = providerId,
        };

        await db.InsertAsync("billing.RefundTransaction", await Refund(null));
        await db.InsertAsync("billing.RefundTransaction", await Refund(null));
        await db.InsertAsync("billing.RefundTransaction", await Refund("rf-1"));

        await db.RejectsAsync(UniqueIndexViolation, "billing.RefundTransaction", await Refund("rf-1"));
    }

    [Fact]
    public async Task ActiveBookingClaim_is_unique_only_while_active()
    {
        await using var db = await OpenAsync();
        var quota = await db.InsertAsync("scheduling.DoctorDayQuota", new { Capacity = 30 });
        var reservation = await db.InsertAsync("scheduling.Reservation", new { SessionQuotaId = quota, BookingAttemptId = await db.InsertAsync("scheduling.BookingAttempt") });
        var (doctor, patient, day) = (Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 11, 10));
        object Claim(bool active) => new
        {
            SessionQuotaId = quota, ReservationId = reservation, DoctorId = doctor, PatientId = patient, ServiceDate = day, IsActive = active,
        };

        await db.InsertAsync("scheduling.ActiveBookingClaim", Claim(false));
        await db.InsertAsync("scheduling.ActiveBookingClaim", Claim(false));
        await db.InsertAsync("scheduling.ActiveBookingClaim", Claim(true));

        await db.RejectsAsync(UniqueIndexViolation, "scheduling.ActiveBookingClaim", Claim(true));
    }

    // ---------- Unique ----------

    [Fact]
    public async Task PatientRepresentative_is_unique_per_user_and_patient()
    {
        await using var db = await OpenAsync();
        var user = await db.InsertAsync("identity.AspNetUsers");
        var patient = await db.InsertAsync("identity.Patient");
        await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = user, PatientId = patient });
        await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = user, PatientId = await db.InsertAsync("identity.Patient") });

        await db.RejectsAsync(UniqueIndexViolation, "identity.PatientRepresentative", new { AppUserId = user, PatientId = patient });
    }

    [Fact]
    public async Task ProcessedEvent_is_unique_per_event_and_handler()
    {
        await using var db = await OpenAsync();
        var eventId = Guid.NewGuid();
        await db.InsertAsync("shared.ProcessedEvent", new { EventId = eventId, HandlerName = "BillingHandler" });
        await db.InsertAsync("shared.ProcessedEvent", new { EventId = eventId, HandlerName = "SchedulingHandler" });

        await db.RejectsAsync(UniqueIndexViolation, "shared.ProcessedEvent", new { EventId = eventId, HandlerName = "BillingHandler" });
    }

    // ---------- Ranh giới module ----------

    [Fact]
    public async Task No_foreign_key_crosses_schemas()
    {
        await using var db = await OpenAsync();
        var crossSchema = await db.QueryAsync("""
            SELECT fk.name FROM sys.foreign_keys fk
            WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) <> OBJECT_SCHEMA_NAME(fk.referenced_object_id)
            """);
        var total = await db.QueryAsync("SELECT COUNT(*) FROM sys.foreign_keys");

        Assert.Empty(crossSchema);
        Assert.NotEqual("0", total.Single());
    }

    private static async Task<Guid> ConsultationPriceListItemAsync(TestDatabase db) =>
        await db.InsertAsync("billing.PriceListItem", new
        {
            PriceListId = await db.InsertAsync("billing.PriceList"), ItemType = "ConsultationFee", DoctorRankId = Guid.NewGuid(),
        });
}
