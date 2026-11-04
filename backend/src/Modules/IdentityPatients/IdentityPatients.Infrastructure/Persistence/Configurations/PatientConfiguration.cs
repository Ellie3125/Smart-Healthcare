using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patient");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerUserId).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.NationalId).HasMaxLength(64);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => x.OwnerUserId);
        builder.HasIndex(x => x.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
    }
}
