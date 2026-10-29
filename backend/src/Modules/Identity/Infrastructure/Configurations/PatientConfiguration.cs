using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Patient), table =>
        {
            table.HasEnumCheck<Gender>(nameof(Patient.Gender));
        });
        builder.Property(e => e.FullName).HasMaxLength(200);
        builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.NationalId).HasMaxLength(20);
        builder.Property(e => e.PhoneNumber).HasMaxLength(20);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.HasIndex(e => e.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
    }
}
