using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class PatientRepresentativeConfiguration : IEntityTypeConfiguration<PatientRepresentative>
{
    public void Configure(EntityTypeBuilder<PatientRepresentative> builder)
    {
        builder.ToTable("PatientRepresentative");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Relationship).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AppUserId, x.PatientId }).IsUnique();
    }
}
