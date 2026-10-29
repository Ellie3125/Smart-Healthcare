using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Thanh toán, dòng phí, phân bổ tiền và hoàn tiền (sơ đồ 5).</summary>
internal static class BillingTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("PaymentIntent", t =>
        {
            t.Id();
            t.Req<string>("IntentNumber", "varchar(30)");
            t.Ref("PayerAppUserId", "AppUser");
            t.Ref("PatientId", "Patient");
            t.OptRef("BookingAttemptId", "BookingAttempt");
            t.OptRef("EncounterId", "Encounter");
            t.Money("TotalExpectedAmount");
            t.Currency();
            t.Enum("Provider", 10, "VNPT", "MoMo", "Counter"); // Counter: đề xuất, chưa chốt
            t.Enum("PaymentType", 20, "InitialConsultation", "CLSOrderBatch");
            t.Enum("Status", 10, "Pending", "Processing", "Succeeded", "Failed", "Unknown", "Expired", "Cancelled").Default("N'Pending'");
            t.UtcNow("CreatedAtUtc");
            t.Utc("ExpiresAtUtc");
            t.RowVersion();
            t.Rule("TotalExpectedAmount", "TotalExpectedAmount > 0");
            // Phí khám ban đầu gắn với BookingAttempt; đợt chỉ định CLS gắn với Encounter.
            t.Check("CK_PaymentIntent_Target", """
                (PaymentType = N'InitialConsultation' AND BookingAttemptId IS NOT NULL AND EncounterId IS NULL)
                OR (PaymentType = N'CLSOrderBatch' AND EncounterId IS NOT NULL AND BookingAttemptId IS NULL)
                """);
            t.Unique("IntentNumber");
        });

        model.Table("PaymentTransaction", t =>
        {
            t.Id();
            t.Ref("PaymentIntentId", "PaymentIntent");
            t.Req<string>("TransactionReference", "varchar(64)");  // Merchant Order ID
            t.Opt<string>("ProviderTransactionId", "varchar(100)"); // Gateway Transaction ID
            t.Money("TransactionAmount");
            t.Currency();
            t.Enum("PaymentStatus", 10, "Pending", "Success", "Failed", "Unknown").Default("N'Pending'");
            t.OptUtc("VerifiedAtUtc");
            t.Opt<string>("ProviderRawData", "nvarchar(max)");
            t.RowVersion();
            t.Rule("TransactionAmount", "TransactionAmount > 0");
            t.Unique("TransactionReference");
            // Cổng thanh toán chỉ trả mã giao dịch sau khi xử lý, nên chỉ duy nhất khi có giá trị.
            t.UniqueWhere("UX_PaymentTransaction_ProviderTransactionId", "ProviderTransactionId IS NOT NULL", "ProviderTransactionId");
        });

        model.Table("PaymentEvent", t =>
        {
            t.Id();
            t.OptRef("PaymentTransactionId", "PaymentTransaction");
            t.Enum("Provider", 10, "VNPT", "MoMo");
            t.Req<string>("EventId", "varchar(100)");
            t.Enum("EventType", 20, "PaymentSuccess", "PaymentRefund");
            t.Req<string>("PayloadHash", "char(64)");
            t.Req<string>("PayloadJson", "nvarchar(max)");
            t.UtcNow("ReceivedAtUtc");
            t.Enum("Status", 20, "Received", "Processed", "DuplicateIgnored", "Error").Default("N'Received'");
            t.OptUtc("ProcessedAtUtc");
            t.Rule("PayloadJson", "ISJSON(PayloadJson) = 1");
            t.UniqueKey("UK_ProviderEvent", "Provider", "EventId");
            t.Index("IX_PaymentEvent_PayloadHash", null, "PayloadHash");
        });

        model.Table("BillingItem", t =>
        {
            t.Id();
            t.OptRef("EncounterId", "Encounter");                 // null trước check-in
            t.OptRef("AppointmentId", "Appointment");             // phí khám ban đầu
            t.OptRef("ServiceOrderItemId", "ServiceOrderItem");   // phí CLS
            t.Ref("PriceListItemId", "PriceListItem");
            t.Enum("ItemType", 20, "ConsultationFee", "CLSService");
            t.Req<string>("ItemNameSnapshot", "nvarchar(200)");
            t.Money("UnitPrice");
            t.Req<int>("Quantity", "int").Default("1");
            t.IndexerProperty<decimal>("TotalAmount").HasPrecision(18, 2)
                .HasComputedColumnSql("CAST(UnitPrice * Quantity AS decimal(18,2))", stored: true);
            t.Money("PaidAmount").Default("0");      // cache, cập nhật cùng transaction
            t.Money("RefundedAmount").Default("0");  // cache, cập nhật cùng transaction
            t.Enum("Status", 15, "Pending", "PartiallyPaid", "Paid", "Cancelled", "Waived").Default("N'Pending'");
            t.RowVersion();
            t.UtcNow("CreatedAtUtc");
            t.Rule("UnitPrice", "UnitPrice >= 0");
            t.Rule("Quantity", "Quantity > 0");
            t.Check("CK_BillingItem_Amounts",
                "PaidAmount >= 0 AND RefundedAmount >= 0 AND RefundedAmount <= PaidAmount AND PaidAmount <= UnitPrice * Quantity");
            t.Check("CK_BillingItem_Target", """
                (ItemType = N'ConsultationFee' AND AppointmentId IS NOT NULL AND ServiceOrderItemId IS NULL)
                OR (ItemType = N'CLSService' AND ServiceOrderItemId IS NOT NULL AND AppointmentId IS NULL)
                """);
            // Quan hệ 0..1 trên sơ đồ: mỗi lịch hẹn / mỗi dòng chỉ định có tối đa một dòng phí.
            t.UniqueWhere("UX_BillingItem_AppointmentId", "AppointmentId IS NOT NULL", "AppointmentId");
            t.UniqueWhere("UX_BillingItem_ServiceOrderItemId", "ServiceOrderItemId IS NOT NULL", "ServiceOrderItemId");
        });

        model.Table("PaymentAllocation", t =>
        {
            t.Id();
            t.Ref("PaymentTransactionId", "PaymentTransaction");
            t.Ref("BillingItemId", "BillingItem");
            t.Money("AllocatedAmount");
            t.Utc("AllocatedAtUtc");
            t.Rule("AllocatedAmount", "AllocatedAmount > 0");
            t.UniqueKey("UQ_PaymentAllocation_TransactionItem", "PaymentTransactionId", "BillingItemId");
        });

        model.Table("RefundRequest", t =>
        {
            t.Id();
            t.Ref("BillingItemId", "BillingItem");
            t.Ref("OriginalPaymentTransactionId", "PaymentTransaction");
            t.OptRef("AppointmentId", "Appointment");
            t.Money("RequestedAmount");
            // 3 giá trị sau CancelledBefore0500: đề xuất, chưa chốt.
            t.Enum("RefundReason", 30, "CancelledBefore0500", "SystemError", "DuplicatePayment", "HospitalCancelled");
            t.Enum("PolicyRuleApplied", 30, "FullRefundPre0500", "CustomApproval");
            t.Enum("Status", 10, "Pending", "Approved", "Rejected", "Settled", "Failed").Default("N'Pending'");
            t.UtcNow("RequestedAtUtc");
            t.OptRef("DecisionByStaffId", "Staff");
            t.OptUtc("DecidedAtUtc");
            t.Rule("RequestedAmount", "RequestedAmount > 0");
        });

        model.Table("RefundTransaction", t =>
        {
            t.Id();
            t.Ref("RefundRequestId", "RefundRequest");
            t.Ref("OriginalPaymentTransactionId", "PaymentTransaction");
            t.Opt<string>("ProviderRefundTransactionId", "varchar(100)");
            t.Money("RefundedAmount");
            t.Currency();
            t.Enum("Status", 10, "Success", "Failed");
            t.Utc("ProcessedAtUtc");
            t.Opt<string>("ProviderResponsePayload", "nvarchar(max)");
            t.Rule("RefundedAmount", "RefundedAmount > 0");
            t.Unique("RefundRequestId");
            t.UniqueWhere("UX_RefundTransaction_ProviderRefundTransactionId", "ProviderRefundTransactionId IS NOT NULL",
                "ProviderRefundTransactionId");
        });
    }
}
