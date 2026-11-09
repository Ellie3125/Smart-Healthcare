namespace Shared.Infrastructure.Persistence.Entities;

public sealed class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string HandlerName { get; set; } = null!;
    public DateTime ProcessedAtUtc { get; set; }
}
