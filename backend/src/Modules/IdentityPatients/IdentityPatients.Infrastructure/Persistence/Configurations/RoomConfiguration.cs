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

        builder.Property(x => x.Code).HasColumnName("Code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RoomType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DepartmentName).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
