using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PriceListItem), table =>
        {
            table.HasEnumCheck<ItemType>(nameof(PriceListItem.ItemType));
            table.HasCheckConstraint("CK_PriceListItem_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_PriceListItem_ItemTypeTarget", "([ItemType] = N'ConsultationFee' AND ([DoctorRankId] IS NOT NULL OR [DoctorId] IS NOT NULL) AND [MedicalServiceId] IS NULL) OR ([ItemType] = N'CLSService' AND [MedicalServiceId] IS NOT NULL AND [DoctorRankId] IS NULL AND [DoctorId] IS NULL)");
        });
        builder.Property(e => e.ItemType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.HasIndex(e => e.DoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.PriceList).WithMany().HasForeignKey(e => e.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DoctorRank).WithMany().HasForeignKey(e => e.DoctorRankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MedicalService).WithMany().HasForeignKey(e => e.MedicalServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
