using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class RoomAccountConfiguration : IEntityTypeConfiguration<RoomAccount>
{
    public void Configure(EntityTypeBuilder<RoomAccount> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AccountCode).HasMaxLength(50);
        builder.HasIndex(e => e.AppUserId).IsUnique();
        builder.HasIndex(e => e.RoomId).IsUnique();
        builder.HasIndex(e => e.AccountCode).IsUnique();
        builder.HasOne(e => e.AppUser).WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Room).WithMany().HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
