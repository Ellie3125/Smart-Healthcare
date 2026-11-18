using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class EncounterClinicalRecordRevisionConfiguration : IEntityTypeConfiguration<EncounterClinicalRecordRevision>
{
    public void Configure(EntityTypeBuilder<EncounterClinicalRecordRevision> builder)
    {
        builder.ToTable("EncounterClinicalRecordRevision", t =>
        {
            t.HasCheckConstraint("CK_EncounterClinicalRecordRevision_RevisionNumber_Positive", "[RevisionNumber] >= 1");
            t.HasCheckConstraint("CK_EncounterClinicalRecordRevision_Kind",
                "([Kind] = N'Published' AND [RevisionNumber] = 1) OR ([Kind] = N'Amended' AND [RevisionNumber] > 1 AND [Reason] IS NOT NULL AND LEN(LTRIM([Reason])) > 0)");
            t.HasCheckConstraint("CK_EncounterClinicalRecordRevision_DiagnosesJson", "ISJSON([DiagnosesJson]) = 1");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ChiefComplaint).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Symptoms).HasColumnType("nvarchar(max)");
        builder.Property(x => x.MedicalHistory).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ClinicalExaminationNotes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.InitialClinicalImpression).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PostCLSConclusion).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TreatmentPlan).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FollowUpInstructions).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DiagnosesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Reason).HasMaxLength(EncounterClinicalRecordRevision.ReasonMaxLength);

        builder.HasOne<EncounterClinicalRecord>().WithMany().HasForeignKey(x => x.ClinicalRecordId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ClinicalRecordId, x.RevisionNumber }).IsUnique();
    }
}
