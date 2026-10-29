using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class PatientRepresentativeConfiguration : IEntityTypeConfiguration<PatientRepresentative>
{
    public void Configure(EntityTypeBuilder<PatientRepresentative> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PatientRepresentative), table =>
        {
            table.HasEnumCheck<Relationship>(nameof(PatientRepresentative.Relationship));
            table.HasEnumCheck<VerificationStatus>(nameof(PatientRepresentative.VerificationStatus));
        });
        builder.Property(e => e.Relationship).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.AuthorizationProofDoc).HasMaxLength(1024);
        builder.Property(e => e.VerificationStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(e => e.AppUser).WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Patient).WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}
