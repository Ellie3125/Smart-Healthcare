using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctor");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Biography).HasColumnType("nvarchar(max)");

        builder.HasOne(x => x.Specialty).WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DoctorRank).WithMany().HasForeignKey(x => x.DoctorRankId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SpecialtyId);
        builder.HasIndex(x => x.DoctorRankId);
        builder.HasIndex(x => x.StaffId).IsUnique().HasFilter("[StaffId] IS NOT NULL");
    }
}
