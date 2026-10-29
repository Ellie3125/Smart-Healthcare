namespace SmartHealthcare.Billing.Domain;


public enum BillingItemStatus
{
    Pending,
    PartiallyPaid,
    Paid,
    Cancelled,
    Waived
}

public enum Currency
{
    VND
}

public enum EventType
{
    PaymentSuccess,
    PaymentRefund
}

public enum ItemType
{
    ConsultationFee,
    CLSService
}

public enum PaymentEventProvider
{
    VNPT,
    MoMo
}

public enum PaymentEventStatus
{
    Received,
    Processed,
    DuplicateIgnored,
    Error
}

public enum PaymentIntentProvider
{
    VNPT,
    MoMo,
    Counter
}

public enum PaymentIntentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Unknown,
    Expired,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failed,
    Unknown
}

public enum PaymentType
{
    InitialConsultation,
    CLSOrderBatch
}

public enum PolicyRuleApplied
{
    FullRefundPre0500,
    CustomApproval
}

public enum RefundReason
{
    CancelledBefore0500,
    PaidUnallocated,
    DuplicatePayment,
    HospitalCancelled,
    SystemError
}

public enum RefundRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Settled,
    Failed
}

public enum RefundTransactionStatus
{
    Success,
    Failed
}
