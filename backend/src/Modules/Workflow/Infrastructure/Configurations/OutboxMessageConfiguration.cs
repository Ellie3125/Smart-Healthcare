using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Workflow.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Workflow.Infrastructure.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(OutboxMessage), table =>
        {
            table.HasEnumCheck<EventType>(nameof(OutboxMessage.EventType));
            table.HasEnumCheck<AggregateType>(nameof(OutboxMessage.AggregateType));
            table.HasEnumCheck<OutboxMessageStatus>(nameof(OutboxMessage.Status));
        });
        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.AggregateType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
