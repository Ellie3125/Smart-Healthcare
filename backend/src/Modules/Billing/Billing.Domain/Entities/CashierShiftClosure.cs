namespace Billing.Domain.Entities;

/// <summary>
/// Biên bản chốt ca thu ngân (Phạm vi nghiệp vụ §5.1, Giải pháp v2.14 "Đối soát cuối ca"): bản chụp tổng tiền mặt hệ thống tính
/// và số tiền tiếp tân đếm được. Một tiếp tân chốt tối đa một lần mỗi ngày (giờ Việt Nam); đã chốt thì không sửa.
/// </summary>
public sealed class CashierShiftClosure
{
    public Guid Id { get; set; }
    // ID chéo module: identity.AppUser của tiếp tân, không FK
    public Guid CashierUserId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public int ReceiptCount { get; set; }
    public int RefundCount { get; set; }
    public decimal ExpectedCashCollected { get; set; }
    public decimal ExpectedCashRefunded { get; set; }
    public decimal ActualCashCounted { get; set; }
    public string Currency { get; set; } = "VND";
    public string? DiscrepancyNote { get; set; }
    public DateTime ClosedAtUtc { get; set; }
}
