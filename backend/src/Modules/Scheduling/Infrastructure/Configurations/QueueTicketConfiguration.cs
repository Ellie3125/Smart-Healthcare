using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(QueueTicket), table =>
        {
            table.HasEnumCheck<TicketType>(nameof(QueueTicket.TicketType));
            table.HasEnumCheck<QueueStatus>(nameof(QueueTicket.QueueStatus));
        });
        builder.Property(e => e.TicketNumber).HasMaxLength(50);
        builder.Property(e => e.TicketType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.QueueStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.EncounterId); // tham chiếu clinical.Encounter, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.TargetRoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.ServiceOrderId); // tham chiếu clinical.ServiceOrder, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.ServiceOrderItemId); // tham chiếu clinical.ServiceOrderItem, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.TargetStation).WithMany().HasForeignKey(e => e.TargetStationId).OnDelete(DeleteBehavior.Restrict);
    }
}
