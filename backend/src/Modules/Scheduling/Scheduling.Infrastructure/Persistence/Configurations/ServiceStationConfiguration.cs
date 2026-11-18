using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class ServiceStationConfiguration : IEntityTypeConfiguration<ServiceStation>
{
    public void Configure(EntityTypeBuilder<ServiceStation> builder)
    {
        builder.ToTable("ServiceStation");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StationCode).HasMaxLength(64);
        builder.Property(x => x.StationName).HasMaxLength(200);

        builder.HasIndex(x => x.StationCode).IsUnique();
        builder.HasIndex(x => x.RoomId);
    }
}
