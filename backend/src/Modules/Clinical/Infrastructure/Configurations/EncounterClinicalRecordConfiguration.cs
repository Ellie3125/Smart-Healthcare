using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class EncounterClinicalRecordConfiguration : IEntityTypeConfiguration<EncounterClinicalRecord>
{
    public void Configure(EntityTypeBuilder<EncounterClinicalRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(EncounterClinicalRecord), table =>
        {
            table.HasEnumCheck<EncounterClinicalRecordStatus>(nameof(EncounterClinicalRecord.Status));
        });
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ChiefComplaint).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Symptoms).HasColumnType("nvarchar(max)");
        builder.Property(e => e.MedicalHistory).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ClinicalExaminationNotes).HasColumnType("nvarchar(max)");
        builder.Property(e => e.InitialClinicalImpression).HasColumnType("nvarchar(max)");
        builder.Property(e => e.PostCLSConclusion).HasColumnType("nvarchar(max)");
        builder.Property(e => e.TreatmentPlan).HasColumnType("nvarchar(max)");
        builder.Property(e => e.FollowUpInstructions).HasColumnType("nvarchar(max)");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.EncounterId).IsUnique();
        builder.HasIndex(e => e.UpdatedByDoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Encounter).WithMany().HasForeignKey(e => e.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
