using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedicationCatalogEntity = MedicationCatalog.Domain.Entities.MedicationCatalog;

namespace MedicationCatalog.Infrastructure.Persistence.Configurations;

internal sealed class MedicationCatalogConfiguration : IEntityTypeConfiguration<MedicationCatalogEntity>
{
    public void Configure(EntityTypeBuilder<MedicationCatalogEntity> builder)
    {
        builder.ToTable("MedicationCatalog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MedicationCode).HasMaxLength(64);
        builder.Property(x => x.MedicationName).HasMaxLength(200);
        builder.Property(x => x.ActiveIngredient).HasMaxLength(200);
        builder.Property(x => x.DosageForm).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Strength).HasMaxLength(200);
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.MedicationCode).IsUnique();
    }
}
