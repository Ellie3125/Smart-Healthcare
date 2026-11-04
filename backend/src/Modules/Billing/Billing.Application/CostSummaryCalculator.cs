using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>
/// Năm chỉ tiêu chi phí của lượt khám (Phạm vi v2.5 §5.5), tính từ các khoản thu.
/// PaidAmount/RefundedAmount là cache cập nhật cùng transaction với giao dịch đã xác minh (ERD v2.4),
/// nên không cộng giao dịch đang chờ, thất bại hay thông báo lặp.
/// </summary>
public static class CostSummaryCalculator
{
    public static EncounterCostSummary Summarize(Guid encounterId, IReadOnlyCollection<BillingItem> items)
    {
        decimal totalPayable = 0, totalPaid = 0, totalRefunded = 0, outstanding = 0, credit = 0;

        foreach (var item in items)
        {
            var net = item.PaidAmount - item.RefundedAmount;
            var obligation = IsInEffect(item.Status) ? item.TotalAmount : 0m;

            totalPayable += obligation;
            totalPaid += item.PaidAmount;
            totalRefunded += item.RefundedAmount;
            outstanding += Math.Max(0m, obligation - net);
            credit += Math.Max(0m, net - obligation);
        }

        return new EncounterCostSummary(
            encounterId,
            BillingCurrency.Vnd,
            totalPayable,
            totalPaid,
            totalRefunded,
            totalPaid - totalRefunded,
            outstanding,
            credit,
            items.OrderBy(i => i.CreatedAtUtc).Select(ChargeMapper.ToDto).ToList());
    }

    /// <summary>Khoản bị hủy hoặc được miễn không còn là nghĩa vụ thanh toán.</summary>
    private static bool IsInEffect(BillingItemStatus status) =>
        status is not (BillingItemStatus.Cancelled or BillingItemStatus.Waived);
}

internal static class ChargeMapper
{
    public static ChargeDto ToDto(BillingItem i) => new(
        i.Id,
        i.ItemType switch
        {
            ItemType.ConsultationFee => ChargeType.ConsultationFee,
            ItemType.CLSService => ChargeType.CLSService,
            _ => throw new ArgumentOutOfRangeException(nameof(i), i.ItemType, null),
        },
        i.PriceListItemId,
        i.ItemNameSnapshot,
        i.UnitPrice,
        i.Quantity,
        i.TotalAmount,
        i.PaidAmount,
        i.RefundedAmount,
        BillingCurrency.Vnd,
        i.Status switch
        {
            BillingItemStatus.Pending => ChargeStatus.Pending,
            BillingItemStatus.PartiallyPaid => ChargeStatus.PartiallyPaid,
            BillingItemStatus.Paid => ChargeStatus.Paid,
            BillingItemStatus.Cancelled => ChargeStatus.Cancelled,
            BillingItemStatus.Waived => ChargeStatus.Waived,
            _ => throw new ArgumentOutOfRangeException(nameof(i), i.Status, null),
        },
        i.EncounterId,
        i.AppointmentId,
        i.ServiceOrderItemId);
}
