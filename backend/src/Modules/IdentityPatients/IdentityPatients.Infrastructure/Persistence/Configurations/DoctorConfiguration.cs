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

        builder.Property(x => x.Biography).HasColumnType("nvarchar(max)");

        builder.HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorRank>().WithMany().HasForeignKey(x => x.DoctorRankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(x => x.DefaultRoomId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);

        builder.HasIndex(x => x.StaffId).IsUnique();
        builder.HasIndex(x => x.SpecialtyId);
        builder.HasIndex(x => x.DoctorRankId);
    }
}
