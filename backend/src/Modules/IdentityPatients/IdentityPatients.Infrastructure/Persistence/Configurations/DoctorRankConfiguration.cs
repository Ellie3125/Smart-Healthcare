using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class DoctorRankConfiguration : IEntityTypeConfiguration<DoctorRank>
{
    public void Configure(EntityTypeBuilder<DoctorRank> builder)
    {
        builder.ToTable("DoctorRank");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnName("Code").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
