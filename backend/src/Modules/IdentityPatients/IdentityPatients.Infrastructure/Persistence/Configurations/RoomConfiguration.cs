using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Room");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoomNumber).HasMaxLength(64);
        builder.Property(x => x.RoomName).HasMaxLength(200);
        builder.Property(x => x.RoomType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.DepartmentName).HasMaxLength(200);

        builder.HasIndex(x => x.RoomNumber).IsUnique();
    }
}
