using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Biography).HasMaxLength(2000);
        builder.HasIndex(e => e.StaffId).IsUnique();
        builder.HasIndex(e => e.SpecialtyId); // tham chiếu catalog.Specialty, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.DoctorRankId); // tham chiếu catalog.DoctorRank, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Staff).WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
