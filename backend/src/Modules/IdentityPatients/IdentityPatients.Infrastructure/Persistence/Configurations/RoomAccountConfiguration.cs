using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class RoomAccountConfiguration : IEntityTypeConfiguration<RoomAccount>
{
    public void Configure(EntityTypeBuilder<RoomAccount> builder)
    {
        builder.ToTable("RoomAccount");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountCode).HasMaxLength(64);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AppUserId).IsUnique();
        builder.HasIndex(x => x.RoomId).IsUnique();
        builder.HasIndex(x => x.AccountCode).IsUnique();
    }
}
