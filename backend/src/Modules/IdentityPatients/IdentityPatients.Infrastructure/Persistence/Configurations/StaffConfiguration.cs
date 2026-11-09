using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StaffCode).HasMaxLength(64);
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.StaffType).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AppUserId).IsUnique();
        builder.HasIndex(x => x.StaffCode).IsUnique();
    }
}
