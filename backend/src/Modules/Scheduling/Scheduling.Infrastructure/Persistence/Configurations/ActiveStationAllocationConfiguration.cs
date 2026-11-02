using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class ActiveStationAllocationConfiguration : IEntityTypeConfiguration<ActiveStationAllocation>
{
    public void Configure(EntityTypeBuilder<ActiveStationAllocation> builder)
    {
        builder.ToTable("ActiveStationAllocation");
        builder.HasKey(x => x.StationId);
        builder.Property(x => x.StationId).ValueGeneratedNever();

        builder.HasOne<ServiceStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QueueTicket>().WithMany().HasForeignKey(x => x.CurrentTicketId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CurrentPatientId).IsUnique();
        builder.HasIndex(x => x.CurrentTicketId).IsUnique();
    }
}
