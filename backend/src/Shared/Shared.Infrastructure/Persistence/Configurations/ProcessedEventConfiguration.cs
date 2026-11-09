using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvent");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HandlerName).HasMaxLength(200);

        builder.HasIndex(x => new { x.EventId, x.HandlerName }).IsUnique();
    }
}
