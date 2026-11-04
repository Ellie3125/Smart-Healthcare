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

        builder.Property(x => x.Code).HasColumnName("Code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
