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

        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.NationalId).HasMaxLength(64);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(200);

        builder.HasIndex(x => x.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        // Tiếp tân tra hồ sơ theo số điện thoại (một số có thể dùng cho nhiều hồ sơ trong gia đình nên không unique).
        builder.HasIndex(x => x.PhoneNumber);
    }
}
