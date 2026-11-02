using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("Specialty");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SpecialtyCode).HasMaxLength(64);
        builder.Property(x => x.SpecialtyName).HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.SpecialtyCode).IsUnique();
    }
}
