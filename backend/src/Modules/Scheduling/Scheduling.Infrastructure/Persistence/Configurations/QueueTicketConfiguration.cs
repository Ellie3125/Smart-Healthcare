using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.ToTable("QueueTicket");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TicketNumber).HasMaxLength(64);
        builder.Property(x => x.TicketType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.QueueStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<ServiceStation>().WithMany().HasForeignKey(x => x.TargetStationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EncounterId);
        builder.HasIndex(x => x.TargetRoomId);
        builder.HasIndex(x => x.ServiceOrderId);
        builder.HasIndex(x => x.ServiceOrderItemId);
    }
}
