using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class BillingItem
{
    public Guid Id { get; set; }
    // ID chéo module: clinical.Encounter, không FK
    public Guid? EncounterId { get; set; }
    // ID chéo module: scheduling.Appointment, không FK
    public Guid? AppointmentId { get; set; }
    // ID chéo module: clinical.ServiceOrderItem, không FK
    public Guid? ServiceOrderItemId { get; set; }
    public Guid PriceListItemId { get; set; }
    public ItemType ItemType { get; set; }
    public string ItemNameSnapshot { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public BillingItemStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public const int ItemNameMaxLength = 200;

    /// <summary>
    /// Tạo khoản thu Pending với đơn giá đã áp dụng (snapshot); thành tiền = đơn giá × số lượng (CK_BillingItem_TotalAmount).
    /// Đổi bảng giá về sau không tính lại khoản này (Phạm vi v2.5 §5.5).
    /// </summary>
    public static BillingItem CreatePending(
        Guid priceListItemId,
        ItemType itemType,
        string itemNameSnapshot,
        decimal unitPrice,
        int quantity,
        DateTime createdAtUtc,
        Guid? encounterId = null,
        Guid? appointmentId = null,
        Guid? serviceOrderItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemNameSnapshot);
        if (itemNameSnapshot.Length > ItemNameMaxLength)
            throw new ArgumentException($"Tên khoản thu tối đa {ItemNameMaxLength} ký tự.", nameof(itemNameSnapshot));
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời điểm phải là UTC.", nameof(createdAtUtc));

        return new BillingItem
        {
            PriceListItemId = priceListItemId,
            ItemType = itemType,
            ItemNameSnapshot = itemNameSnapshot,
            UnitPrice = unitPrice,
            Quantity = quantity,
            TotalAmount = unitPrice * quantity,
            PaidAmount = 0m,
            RefundedAmount = 0m,
            Status = BillingItemStatus.Pending,
            CreatedAtUtc = createdAtUtc,
            EncounterId = encounterId,
            AppointmentId = appointmentId,
            ServiceOrderItemId = serviceOrderItemId,
        };
    }

    /// <summary>Khoản còn là nghĩa vụ thanh toán: không bị hủy, không được miễn.</summary>
    public bool IsInEffect => Status is not (BillingItemStatus.Cancelled or BillingItemStatus.Waived);

    /// <summary>Phần còn phải trả = thành tiền − (đã trả − đã hoàn); 0 với khoản đã hủy hoặc miễn.</summary>
    public decimal Outstanding => IsInEffect ? Math.Max(0m, TotalAmount - (PaidAmount - RefundedAmount)) : 0m;

    /// <summary>
    /// Ghi nhận tiền của một giao dịch đã xác minh vào khoản (PaidAmount là cache, cập nhật cùng transaction — ERD v2.4).
    /// Đủ thành tiền thì Paid, chưa đủ thì PartiallyPaid. Khoản đã hủy/miễn vẫn nhận tiền để theo dõi tiền chờ hoàn.
    /// </summary>
    public void ApplyPayment(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        PaidAmount += amount;
        if (IsInEffect)
            Status = PaidAmount - RefundedAmount >= TotalAmount ? BillingItemStatus.Paid : BillingItemStatus.PartiallyPaid;
    }
}
