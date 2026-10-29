using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PrescriptionItem), table =>
        {
            table.HasEnumCheck<Route>(nameof(PrescriptionItem.Route));
        });
        builder.Property(e => e.MedicationNameSnapshot).HasMaxLength(200);
        builder.Property(e => e.ActiveIngredientSnapshot).HasMaxLength(200);
        builder.Property(e => e.Dosage).HasMaxLength(200);
        builder.Property(e => e.Route).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Frequency).HasMaxLength(200);
        builder.Property(e => e.UsageInstructions).HasMaxLength(2000);
        builder.HasIndex(e => e.MedicationId); // tham chiếu catalog.MedicationCatalog, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Prescription).WithMany().HasForeignKey(e => e.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}
