namespace Billing.Contracts;

/// <summary>Loại tiền duy nhất trong phạm vi đồ án (API-04).</summary>
public static class BillingCurrency
{
    public const string Vnd = "VND";
}

public enum ChargeType
{
    ConsultationFee,
    CLSService,
}

/// <summary>Căn cứ chọn đơn giá.</summary>
public enum PriceBasis
{
    /// <summary>Giá riêng của bác sĩ (PriceListItem.DoctorId).</summary>
    Doctor,

    /// <summary>Giá theo cấp bậc bác sĩ (PriceListItem.DoctorRankId).</summary>
    DoctorRank,

    /// <summary>Giá dịch vụ CLS (PriceListItem.MedicalServiceId).</summary>
    MedicalService,
}

public enum ChargeStatus
{
    Pending,
    PartiallyPaid,
    Paid,
    Cancelled,
    Waived,
}

/// <param name="AtUtc">Thời điểm áp giá, UTC (Kind = Utc).</param>
public sealed record QuoteExamFee(Guid DoctorId, Guid DoctorRankId, DateTime AtUtc);

/// <param name="MedicalServiceIds">Không rỗng, không trùng.</param>
/// <param name="AtUtc">Thời điểm áp giá, UTC (Kind = Utc).</param>
public sealed record QuoteClsFees(IReadOnlyList<Guid> MedicalServiceIds, DateTime AtUtc);

public sealed record FeeQuote(
    Guid PriceListItemId,
    string PriceListCode,
    ChargeType ChargeType,
    PriceBasis Basis,
    Guid? MedicalServiceId,
    decimal UnitPrice,
    string Currency);

/// <param name="ItemNameSnapshot">Tên khoản thu hiển thị, tối đa 200 ký tự, ví dụ "Phí khám - BS. Nguyễn Văn A".</param>
/// <param name="PricedAtUtc">Thời điểm áp giá, UTC; thường là lúc giữ chỗ.</param>
public sealed record CreateExamCharge(
    Guid DoctorId,
    Guid DoctorRankId,
    string ItemNameSnapshot,
    DateTime PricedAtUtc);

/// <param name="PricedAtUtc">Thời điểm áp giá, UTC; thường là lúc bác sĩ chỉ định.</param>
public sealed record CreateClsCharges(
    Guid EncounterId,
    DateTime PricedAtUtc,
    IReadOnlyList<ClsChargeLine> Lines);

/// <param name="ServiceNameSnapshot">Tên dịch vụ tại thời điểm chỉ định, tối đa 200 ký tự.</param>
/// <param name="Quantity">Lớn hơn 0. Billing tự tra đơn giá đang hiệu lực.</param>
public sealed record ClsChargeLine(
    Guid ServiceOrderItemId,
    Guid MedicalServiceId,
    string ServiceNameSnapshot,
    int Quantity);

public sealed record ChargeDto(
    Guid ChargeId,
    ChargeType ChargeType,
    Guid PriceListItemId,
    string ItemNameSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RefundedAmount,
    string Currency,
    ChargeStatus Status,
    Guid? EncounterId,
    Guid? AppointmentId,
    Guid? ServiceOrderItemId);

/// <param name="TotalPayable">Tổng thành tiền các khoản còn hiệu lực (không tính Cancelled, Waived).</param>
/// <param name="TotalPaid">Tổng đã thanh toán đã xác minh.</param>
/// <param name="TotalRefunded">Tổng đã hoàn đã xác minh.</param>
/// <param name="NetCollected">TotalPaid - TotalRefunded.</param>
/// <param name="Outstanding">Phần nghĩa vụ còn phải trả của các khoản còn hiệu lực.</param>
/// <param name="CreditPendingRefund">Tiền thu thừa hoặc đã thu của khoản bị hủy/miễn mà chưa hoàn, theo dõi riêng.</param>
public sealed record EncounterCostSummary(
    Guid EncounterId,
    string Currency,
    decimal TotalPayable,
    decimal TotalPaid,
    decimal TotalRefunded,
    decimal NetCollected,
    decimal Outstanding,
    decimal CreditPendingRefund,
    IReadOnlyList<ChargeDto> Charges);
