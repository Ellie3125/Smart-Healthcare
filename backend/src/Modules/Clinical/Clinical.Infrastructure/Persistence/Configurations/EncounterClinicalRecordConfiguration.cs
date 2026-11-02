using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class EncounterClinicalRecordConfiguration : IEntityTypeConfiguration<EncounterClinicalRecord>
{
    public void Configure(EntityTypeBuilder<EncounterClinicalRecord> builder)
    {
        builder.ToTable("EncounterClinicalRecord");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ChiefComplaint).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Symptoms).HasColumnType("nvarchar(max)");
        builder.Property(x => x.MedicalHistory).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ClinicalExaminationNotes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.InitialClinicalImpression).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PostCLSConclusion).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TreatmentPlan).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FollowUpInstructions).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<Encounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EncounterId).IsUnique();
        builder.HasIndex(x => x.UpdatedByDoctorId);
    }
}
