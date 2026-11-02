using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class BillingItemConfiguration : IEntityTypeConfiguration<BillingItem>
{
    public void Configure(EntityTypeBuilder<BillingItem> builder)
    {
        builder.ToTable("BillingItem");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ItemNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<PriceListItem>().WithMany().HasForeignKey(x => x.PriceListItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EncounterId);
        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => x.ServiceOrderItemId);
    }
}
