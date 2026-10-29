using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Prescription), table =>
        {
            table.HasEnumCheck<PrescriptionStatus>(nameof(Prescription.Status));
        });
        builder.Property(e => e.PrescriptionCode).HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.GeneralInstructions).HasMaxLength(2000);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.PrescribedByDoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PrescriptionCode).IsUnique();
        builder.HasOne(e => e.Encounter).WithMany().HasForeignKey(e => e.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
