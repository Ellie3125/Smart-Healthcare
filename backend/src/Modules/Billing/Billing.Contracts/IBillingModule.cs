namespace Billing.Contracts;

/// <summary>
/// Contract trong tiến trình do Billing công bố (docs/api/billing-module-contract.md, v0.1 Draft).
/// Module khác chỉ tham chiếu project này. Đơn giá luôn do Billing tra trong bảng giá của mình;
/// không nhận đơn giá hay số tiền từ bên gọi (BR-001, POL-10).
/// </summary>
public interface IBillingModule
{
    /// <summary>Báo giá phí khám ban đầu cho bác sĩ tại thời điểm AtUtc: giá riêng của bác sĩ, nếu không có thì giá theo cấp bậc.</summary>
    Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct);

    /// <summary>Báo giá các dịch vụ CLS tại thời điểm AtUtc, mỗi dịch vụ một dòng theo thứ tự yêu cầu.</summary>
    Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct);

    /// <summary>Tạo khoản thu phí khám ban đầu (Pending) với đơn giá đang hiệu lực tại PricedAtUtc.</summary>
    Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct);

    /// <summary>
    /// Tạo khoản thu phí CLS cho đợt chỉ định. Gọi lại với ServiceOrderItemId đã có khoản thu còn hiệu lực
    /// thì trả khoản cũ, không tạo thêm.
    /// </summary>
    Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct);

    /// <summary>Tổng hợp chi phí của một lượt khám theo năm chỉ tiêu ở Phạm vi nghiệp vụ v2.5 §5.5.</summary>
    Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct);
}
