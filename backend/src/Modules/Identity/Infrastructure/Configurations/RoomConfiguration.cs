using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Room), table =>
        {
            table.HasEnumCheck<RoomType>(nameof(Room.RoomType));
        });
        builder.Property(e => e.RoomNumber).HasMaxLength(50);
        builder.Property(e => e.RoomName).HasMaxLength(200);
        builder.Property(e => e.RoomType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.DepartmentName).HasMaxLength(200);
        builder.HasIndex(e => e.RoomNumber).IsUnique();
    }
}
