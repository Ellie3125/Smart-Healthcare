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

        builder.Property(x => x.RankCode).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RankName).HasMaxLength(200);

        builder.HasIndex(x => x.RankCode).IsUnique();
    }
}
