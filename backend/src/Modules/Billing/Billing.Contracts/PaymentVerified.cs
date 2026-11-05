using Shared.Abstractions;

namespace Billing.Contracts;

public enum PaymentPurpose
{
    /// <summary>Phí khám ban đầu lúc đặt lịch; Scheduling xác nhận reservation của BookingAttemptId.</summary>
    InitialConsultation,

    /// <summary>Phí CLS của một đợt chỉ định; Scheduling mở điều kiện bắt đầu cho đúng các vé có khoản trong ChargeIds.</summary>
    CLSOrderBatch,
}

/// <summary>
/// Billing phát sau khi đã xác minh giao dịch thành công (chữ ký, số tiền, người nhận, mã tham chiếu — BR-002)
/// và đã ghi Paid trong cùng transaction với Outbox. Phát lặp được; bên nhận chống lặp bằng ProcessedEvent.
/// </summary>
/// <param name="BookingAttemptId">Có khi Purpose = InitialConsultation; Scheduling suy Reservation từ đây.</param>
/// <param name="EncounterId">Có khi Purpose = CLSOrderBatch.</param>
/// <param name="ChargeIds">Các khoản thu (BillingItem) đã được giao dịch này thanh toán đủ.</param>
public sealed record PaymentVerified(
    Guid EventId,
    Guid PaymentIntentId,
    PaymentPurpose Purpose,
    Guid PatientId,
    Guid? BookingAttemptId,
    Guid? EncounterId,
    IReadOnlyList<Guid> ChargeIds,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;

    public int Version => CurrentVersion;
}
