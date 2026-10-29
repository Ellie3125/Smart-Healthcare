using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Staff), table =>
        {
            table.HasEnumCheck<StaffType>(nameof(Staff.StaffType));
        });
        builder.Property(e => e.StaffCode).HasMaxLength(50);
        builder.Property(e => e.FullName).HasMaxLength(200);
        builder.Property(e => e.StaffType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.AppUserId).IsUnique();
        builder.HasIndex(e => e.StaffCode).IsUnique();
        builder.HasOne(e => e.AppUser).WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
