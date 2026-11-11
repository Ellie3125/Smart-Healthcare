using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class EncounterDiagnosisConfiguration : IEntityTypeConfiguration<EncounterDiagnosis>
{
    public void Configure(EntityTypeBuilder<EncounterDiagnosis> builder)
    {
        builder.ToTable("EncounterDiagnosis");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DiagnosisType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ICD10Code).HasMaxLength(64);
        builder.Property(x => x.DiagnosisName).HasMaxLength(200);
        builder.Property(x => x.ClinicalNotes).HasColumnType("nvarchar(max)");

        builder.HasOne<Encounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
