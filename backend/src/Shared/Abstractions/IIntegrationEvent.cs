namespace Shared.Abstractions;

/// <summary>
/// Marker interface for integration events phát qua Outbox.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

