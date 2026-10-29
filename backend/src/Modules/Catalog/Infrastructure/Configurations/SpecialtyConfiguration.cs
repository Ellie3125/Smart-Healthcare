using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SpecialtyCode).HasMaxLength(50);
        builder.Property(e => e.SpecialtyName).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.HasIndex(e => e.SpecialtyCode).IsUnique();
    }
}
