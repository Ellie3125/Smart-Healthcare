using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class ServiceStationConfiguration : IEntityTypeConfiguration<ServiceStation>
{
    public void Configure(EntityTypeBuilder<ServiceStation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StationCode).HasMaxLength(50);
        builder.Property(e => e.StationName).HasMaxLength(200);
        builder.HasIndex(e => e.RoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.StationCode).IsUnique();
    }
}
