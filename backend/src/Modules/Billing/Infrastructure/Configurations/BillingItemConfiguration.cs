using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class BillingItemConfiguration : IEntityTypeConfiguration<BillingItem>
{
    public void Configure(EntityTypeBuilder<BillingItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(BillingItem), table =>
        {
            table.HasEnumCheck<ItemType>(nameof(BillingItem.ItemType));
            table.HasEnumCheck<BillingItemStatus>(nameof(BillingItem.Status));
            table.HasCheckConstraint("CK_BillingItem_TotalAmount", "[TotalAmount] = [UnitPrice] * [Quantity]");
        });
        builder.Property(e => e.ItemType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ItemNameSnapshot).HasMaxLength(200);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.PaidAmount).HasPrecision(18, 2);
        builder.Property(e => e.RefundedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.EncounterId); // tham chiếu clinical.Encounter, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.AppointmentId); // tham chiếu scheduling.Appointment, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.ServiceOrderItemId); // tham chiếu clinical.ServiceOrderItem, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PriceListItemId); // tham chiếu catalog.PriceListItem, không FK chéo schema (MOD-01)
    }
}
