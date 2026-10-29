using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class RoomDeviceSessionConfiguration : IEntityTypeConfiguration<RoomDeviceSession>
{
    public void Configure(EntityTypeBuilder<RoomDeviceSession> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DeviceId).HasMaxLength(128);
        builder.Property(e => e.DeviceIpAddress).HasMaxLength(45);
        builder.Property(e => e.SessionToken).HasMaxLength(64);
        builder.HasIndex(e => e.SessionToken).IsUnique();
        builder.HasOne(e => e.RoomAccount).WithMany().HasForeignKey(e => e.RoomAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
