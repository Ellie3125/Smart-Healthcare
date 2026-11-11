using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.ToTable("VitalSign");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WeightKg).HasPrecision(5, 2);
        builder.Property(x => x.HeightCm).HasPrecision(5, 1);
        builder.Property(x => x.SystolicBP).HasPrecision(6, 2);
        builder.Property(x => x.DiastolicBP).HasPrecision(6, 2);
        builder.Property(x => x.BodyTemperature).HasPrecision(4, 1);
        builder.Property(x => x.SpO2).HasPrecision(5, 2);

        builder.HasOne<Encounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RecordedByStaffId);
    }
}
