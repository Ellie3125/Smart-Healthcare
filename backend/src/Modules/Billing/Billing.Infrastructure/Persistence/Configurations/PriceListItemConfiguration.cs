using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.ToTable("PriceListItem", t =>
        {
            t.HasCheckConstraint("CK_PriceListItem_UnitPrice_NonNegative", "[UnitPrice] >= 0");
            t.HasCheckConstraint("CK_PriceListItem_ItemType_Targets", "([ItemType] = 'ConsultationFee' AND [MedicalServiceId] IS NULL AND ([DoctorRankId] IS NOT NULL OR [DoctorId] IS NOT NULL)) OR ([ItemType] = 'CLSService' AND [MedicalServiceId] IS NOT NULL AND [DoctorRankId] IS NULL AND [DoctorId] IS NULL)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DoctorRankId);
        builder.HasIndex(x => x.DoctorId);
        builder.HasIndex(x => x.MedicalServiceId);
    }
}
