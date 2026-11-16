using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Xunit;

namespace Billing.Tests;

public class ClsBatchesTests
{
    private static readonly Guid Encounter = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Order = Guid.NewGuid();

    // 09:00 ngày 16/11/2026 giờ Việt Nam.
    private static readonly DateTime Ordered = new(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-11-16T02:00:00Z", "2026-11-16T17:00:00Z")] // 09:00 VN → hết ngày 16/11 VN
    [InlineData("2026-11-16T16:59:59Z", "2026-11-16T17:00:00Z")] // 23:59:59 VN, vẫn ngày 16/11
    [InlineData("2026-11-16T17:00:00Z", "2026-11-17T17:00:00Z")] // 00:00 VN ngày 17/11
    [InlineData("2026-11-15T20:00:00Z", "2026-11-16T17:00:00Z")] // 03:00 VN ngày 16/11, UTC còn ngày 15
    public void Pay_by_is_end_of_the_vietnam_day(string orderedAt, string expected)
    {
        var payBy = ClsBatches.PayByUtc(DateTime.Parse(orderedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        Assert.Equal(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), payBy);
        Assert.Equal(DateTimeKind.Utc, payBy.Kind);
    }

    [Fact]
    public void Unpaid_batch_is_pending_with_totals()
    {
        var batch = ClsBatches.Build([Charge(200_000m), Charge(120_000m, quantity: 2)], Ordered.AddHours(1));

        Assert.Equal(ClsBatchStatus.PendingPayment, batch.Status);
        Assert.True(batch.CanPay);
        Assert.Equal(440_000m, batch.TotalAmount);
        Assert.Equal(0m, batch.PaidAmount);
        Assert.Equal(440_000m, batch.Outstanding);
        Assert.Equal((Order, Encounter, Patient), (batch.ServiceOrderId, batch.EncounterId, batch.PatientId));
        Assert.Equal(Ordered, batch.OrderedAtUtc);
        Assert.Equal(new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc), batch.PayByUtc);
        Assert.Equal(2, batch.Items.Count);
    }

    [Fact]
    public void Partly_paid_batch_asks_only_for_the_rest()
    {
        var paid = Charge(200_000m);
        paid.ApplyPayment(200_000m);

        var batch = ClsBatches.Build([paid, Charge(120_000m)], Ordered.AddHours(1));

        Assert.Equal(ClsBatchStatus.PendingPayment, batch.Status);
        Assert.Equal(200_000m, batch.PaidAmount);
        Assert.Equal(120_000m, batch.Outstanding);
        Assert.Equal([0m, 120_000m], batch.Items.Select(i => i.Outstanding).Order());
    }

    [Fact]
    public void Fully_paid_batch_is_paid_even_after_the_day()
    {
        var a = Charge(200_000m);
        a.ApplyPayment(200_000m);

        var batch = ClsBatches.Build([a], Ordered.AddDays(2));

        Assert.Equal(ClsBatchStatus.Paid, batch.Status);
        Assert.False(batch.CanPay);
    }

    [Fact]
    public void Unpaid_batch_after_the_day_is_expired()
    {
        var batch = ClsBatches.Build([Charge(200_000m)], new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ClsBatchStatus.Expired, batch.Status);
        Assert.False(batch.CanPay);
    }

    [Fact]
    public void Cancelled_and_waived_charges_do_not_count()
    {
        var cancelled = Charge(500_000m);
        cancelled.Status = BillingItemStatus.Cancelled;
        var waived = Charge(90_000m);
        waived.Status = BillingItemStatus.Waived;

        var mixed = ClsBatches.Build([cancelled, waived, Charge(120_000m)], Ordered);
        var allCancelled = ClsBatches.Build([cancelled, waived], Ordered);

        Assert.Equal(120_000m, mixed.TotalAmount);
        Assert.Equal(120_000m, mixed.Outstanding);
        Assert.Equal(3, mixed.Items.Count);
        Assert.Contains(mixed.Items, i => i.Status == ChargeStatus.Cancelled && i.Outstanding == 0m);
        Assert.Equal(ClsBatchStatus.Cancelled, allCancelled.Status);
        Assert.False(allCancelled.CanPay);
    }

    private static BillingItem Charge(decimal price, int quantity = 1)
    {
        var item = BillingItem.CreatePending(Guid.NewGuid(), ItemType.CLSService, "Dịch vụ CLS giả lập", price, quantity, Ordered,
            encounterId: Encounter, serviceOrderItemId: Guid.NewGuid(), serviceOrderId: Order, patientId: Patient);
        item.Id = Guid.NewGuid();
        return item;
    }
}
