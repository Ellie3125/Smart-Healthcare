using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class CashierShiftClosureConfiguration : IEntityTypeConfiguration<CashierShiftClosure>
{
    public void Configure(EntityTypeBuilder<CashierShiftClosure> builder)
    {
        builder.ToTable("CashierShiftClosure", t =>
        {
            t.HasCheckConstraint("CK_CashierShiftClosure_Amounts",
                "[ExpectedCashCollected] >= 0 AND [ExpectedCashRefunded] >= 0 AND [ActualCashCounted] >= 0 AND [ReceiptCount] >= 0 AND [RefundCount] >= 0");
            // Lệch két phải có lý do. IS NOT NULL bắt buộc: CHECK cho qua khi biểu thức là UNKNOWN (LEN(NULL)).
            t.HasCheckConstraint("CK_CashierShiftClosure_DiscrepancyNote",
                "[ActualCashCounted] = [ExpectedCashCollected] - [ExpectedCashRefunded] OR ([DiscrepancyNote] IS NOT NULL AND LEN(LTRIM(RTRIM([DiscrepancyNote]))) > 0)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpectedCashCollected).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCashRefunded).HasPrecision(18, 2);
        builder.Property(x => x.ActualCashCounted).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(64);
        builder.Property(x => x.DiscrepancyNote).HasMaxLength(500);

        // Chốt lại cùng ngày bị chặn ở DB, không chỉ ở API (409).
        builder.HasIndex(x => new { x.CashierUserId, x.ShiftDate }).IsUnique();
    }
}
