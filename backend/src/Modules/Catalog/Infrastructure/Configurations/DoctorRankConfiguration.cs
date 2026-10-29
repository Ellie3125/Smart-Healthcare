using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class DoctorRankConfiguration : IEntityTypeConfiguration<DoctorRank>
{
    public void Configure(EntityTypeBuilder<DoctorRank> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(DoctorRank), table =>
        {
            table.HasEnumCheck<RankCode>(nameof(DoctorRank.RankCode));
        });
        builder.Property(e => e.RankCode).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RankName).HasMaxLength(200);
        builder.HasIndex(e => e.RankCode).IsUnique();
    }
}
