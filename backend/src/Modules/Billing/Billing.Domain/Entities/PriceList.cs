namespace Billing.Domain.Entities;

public sealed class PriceList
{
    public Guid Id { get; set; }
    public string PriceListCode { get; set; } = null!;
    public string PriceListName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Bảng giá áp dụng tại thời điểm atUtc: đang bật và EffectiveFromUtc ≤ atUtc &lt; EffectiveToUtc (null = không hết hạn).</summary>
    public bool IsEffectiveAt(DateTime atUtc) =>
        IsActive && EffectiveFromUtc <= atUtc && (EffectiveToUtc is null || atUtc < EffectiveToUtc);
}
