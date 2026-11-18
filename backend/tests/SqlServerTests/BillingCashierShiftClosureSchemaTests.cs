using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Ràng buộc DB của biên bản chốt ca: mỗi tiếp tân chốt một lần mỗi ngày, số tiền không âm, lệch két phải có ghi chú.
// Chạy trong transaction của TestDatabase rồi rollback.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingCashierShiftClosureSchemaTests
{
    private const string Table = "billing.CashierShiftClosure";

    private static object Row(Guid cashier, decimal counted, string? note = null, decimal collected = 500_000m, decimal refunded = 150_000m) => new
    {
        Id = Guid.NewGuid(),
        CashierUserId = cashier,
        ShiftDate = new DateTime(2026, 11, 18),
        ReceiptCount = 3,
        RefundCount = 1,
        ExpectedCashCollected = collected,
        ExpectedCashRefunded = refunded,
        ActualCashCounted = counted,
        Currency = "VND",
        DiscrepancyNote = note,
        ClosedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task Matching_count_needs_no_note_but_second_close_same_day_is_rejected()
    {
        await using var db = await OpenAsync();
        var cashier = Guid.NewGuid();

        await db.InsertAsync(Table, Row(cashier, 350_000m));

        await db.RejectsAsync(2601, Table, Row(cashier, 350_000m));
        // Tiếp tân khác cùng ngày vẫn chốt được.
        await db.InsertAsync(Table, Row(Guid.NewGuid(), 350_000m));
    }

    [Fact]
    public async Task Discrepancy_requires_a_note_and_amounts_cannot_be_negative()
    {
        await using var db = await OpenAsync();

        await db.RejectsAsync(547, Table, Row(Guid.NewGuid(), 340_000m));
        await db.RejectsAsync(547, Table, Row(Guid.NewGuid(), 340_000m, "   "));
        await db.InsertAsync(Table, Row(Guid.NewGuid(), 340_000m, "Thiếu 10.000 do trả nhầm tiền thừa"));

        await db.RejectsAsync(547, Table, Row(Guid.NewGuid(), -1m, "âm"));
        await db.RejectsAsync(547, Table, Row(Guid.NewGuid(), 0m, "x", collected: -5m, refunded: 0m));
    }
}
