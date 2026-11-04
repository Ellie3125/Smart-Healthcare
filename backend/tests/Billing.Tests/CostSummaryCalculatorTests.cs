using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Xunit;

namespace Billing.Tests;

public class CostSummaryCalculatorTests
{
    private static readonly Guid Encounter = Guid.NewGuid();
    private static readonly DateTime Now = new(2027, 1, 15, 2, 0, 0, DateTimeKind.Utc);

    private static BillingItem Item(decimal total, decimal paid, decimal refunded, BillingItemStatus status, ItemType type = ItemType.CLSService)
    {
        var item = BillingItem.CreatePending(Guid.NewGuid(), type, "Khoản thu giả lập", total, 1, Now, encounterId: Encounter);
        item.PaidAmount = paid;
        item.RefundedAmount = refunded;
        item.Status = status;
        return item;
    }

    [Fact]
    public void Example_from_scope_150k_exam_plus_200k_ultrasound()
    {
        // Phạm vi v2.5 §5.5: phí khám 150.000 + siêu âm 200.000 = 350.000; mới trả phí khám.
        var summary = CostSummaryCalculator.Summarize(Encounter,
        [
            Item(150_000m, 150_000m, 0m, BillingItemStatus.Paid, ItemType.ConsultationFee),
            Item(200_000m, 0m, 0m, BillingItemStatus.Pending),
        ]);

        Assert.Equal(350_000m, summary.TotalPayable);
        Assert.Equal(150_000m, summary.TotalPaid);
        Assert.Equal(0m, summary.TotalRefunded);
        Assert.Equal(150_000m, summary.NetCollected);
        Assert.Equal(200_000m, summary.Outstanding);
        Assert.Equal(0m, summary.CreditPendingRefund);
        Assert.Equal(2, summary.Charges.Count);
        Assert.Equal("VND", summary.Currency);
    }

    [Fact]
    public void Cancelled_and_waived_items_are_not_payable()
    {
        var summary = CostSummaryCalculator.Summarize(Encounter,
        [
            Item(200_000m, 0m, 0m, BillingItemStatus.Cancelled),
            Item(90_000m, 0m, 0m, BillingItemStatus.Waived),
        ]);

        Assert.Equal(0m, summary.TotalPayable);
        Assert.Equal(0m, summary.Outstanding);
    }

    [Fact]
    public void Paid_then_cancelled_item_shows_as_credit_until_refunded()
    {
        var notRefunded = CostSummaryCalculator.Summarize(Encounter, [Item(200_000m, 200_000m, 0m, BillingItemStatus.Cancelled)]);
        Assert.Equal(0m, notRefunded.TotalPayable);
        Assert.Equal(200_000m, notRefunded.CreditPendingRefund);
        Assert.Equal(0m, notRefunded.Outstanding);

        var refunded = CostSummaryCalculator.Summarize(Encounter, [Item(200_000m, 200_000m, 200_000m, BillingItemStatus.Cancelled)]);
        Assert.Equal(200_000m, refunded.TotalPaid);
        Assert.Equal(200_000m, refunded.TotalRefunded);
        Assert.Equal(0m, refunded.NetCollected);
        Assert.Equal(0m, refunded.CreditPendingRefund);
    }

    [Fact]
    public void Partial_payment_leaves_remaining_outstanding()
    {
        var summary = CostSummaryCalculator.Summarize(Encounter, [Item(200_000m, 50_000m, 0m, BillingItemStatus.PartiallyPaid)]);
        Assert.Equal(150_000m, summary.Outstanding);
    }

    [Fact]
    public void Empty_encounter_has_zero_totals()
    {
        var summary = CostSummaryCalculator.Summarize(Encounter, []);
        Assert.Equal(0m, summary.TotalPayable);
        Assert.Empty(summary.Charges);
    }
}
