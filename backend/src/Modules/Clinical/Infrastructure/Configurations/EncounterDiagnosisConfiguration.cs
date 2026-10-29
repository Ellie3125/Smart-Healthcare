using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class EncounterDiagnosisConfiguration : IEntityTypeConfiguration<EncounterDiagnosis>
{
    public void Configure(EntityTypeBuilder<EncounterDiagnosis> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(EncounterDiagnosis), table =>
        {
            table.HasEnumCheck<DiagnosisType>(nameof(EncounterDiagnosis.DiagnosisType));
        });
        builder.Property(e => e.DiagnosisType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ICD10Code).HasMaxLength(10);
        builder.Property(e => e.DiagnosisName).HasMaxLength(500);
        builder.Property(e => e.ClinicalNotes).HasColumnType("nvarchar(max)");
        builder.HasOne(e => e.Encounter).WithMany().HasForeignKey(e => e.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
