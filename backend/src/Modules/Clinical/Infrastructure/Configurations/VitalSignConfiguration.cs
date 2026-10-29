using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WeightKg).HasPrecision(5, 2);
        builder.Property(e => e.HeightCm).HasPrecision(5, 2);
        builder.Property(e => e.SystolicBP).HasPrecision(5, 2);
        builder.Property(e => e.DiastolicBP).HasPrecision(5, 2);
        builder.Property(e => e.BodyTemperature).HasPrecision(5, 2);
        builder.Property(e => e.SpO2).HasPrecision(5, 2);
        builder.HasIndex(e => e.RecordedByStaffId); // tham chiếu identity.Staff, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Encounter).WithMany().HasForeignKey(e => e.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
