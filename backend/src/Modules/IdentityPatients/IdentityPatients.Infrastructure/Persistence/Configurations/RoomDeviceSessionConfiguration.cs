using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class RoomDeviceSessionConfiguration : IEntityTypeConfiguration<RoomDeviceSession>
{
    public void Configure(EntityTypeBuilder<RoomDeviceSession> builder)
    {
        builder.ToTable("RoomDeviceSession");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).HasMaxLength(64);
        builder.Property(x => x.DeviceIpAddress).HasMaxLength(64);
        builder.Property(x => x.SessionToken).HasMaxLength(64);

        builder.HasOne<RoomAccount>().WithMany().HasForeignKey(x => x.RoomAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SessionToken).IsUnique();
        builder.HasIndex(x => x.RoomAccountId).IsUnique().HasFilter("[IsActive] = 1");
    }
}
