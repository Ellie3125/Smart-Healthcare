using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class MedicationCatalogConfiguration : IEntityTypeConfiguration<MedicationCatalog>
{
    public void Configure(EntityTypeBuilder<MedicationCatalog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(MedicationCatalog), table =>
        {
            table.HasEnumCheck<DosageForm>(nameof(MedicationCatalog.DosageForm));
            table.HasEnumCheck<Unit>(nameof(MedicationCatalog.Unit));
        });
        builder.Property(e => e.MedicationCode).HasMaxLength(50);
        builder.Property(e => e.MedicationName).HasMaxLength(200);
        builder.Property(e => e.ActiveIngredient).HasMaxLength(200);
        builder.Property(e => e.DosageForm).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Strength).HasMaxLength(200);
        builder.Property(e => e.Unit).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.MedicationCode).IsUnique();
    }
}
