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
}
