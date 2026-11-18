using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Ghi integration event vào shared.OutboxMessage trong CÙNG transaction với DbContext của module nghiệp vụ (MSG-02):
/// SharedDbContext dùng chung DbConnection và DbTransaction của module, nên commit/rollback đi cùng nhau.
/// OutboxMessage.Id = EventId của sự kiện. Worker phát Outbox và chống lặp bằng ProcessedEvent.
/// </summary>
public sealed class OutboxWriter(SharedDbContext shared, TimeProvider clock)
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public async Task EnqueueAsync(DbContext owner, IIntegrationEvent integrationEvent, EventType eventType,
        AggregateType aggregateType, Guid aggregateId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var transaction = (owner.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Outbox phải được ghi bên trong transaction nghiệp vụ (MSG-02).")).GetDbTransaction();

        await JoinAsync(transaction, ct);
        shared.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            OperationId = integrationEvent.OperationId,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions),
            RetryCount = 0,
            Status = OutboxMessageStatus.Pending,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
        await shared.SaveChangesAsync(ct);
    }

    /// <summary>Bỏ các bản ghi chưa lưu khi transaction nghiệp vụ bị rollback để chạy lại (DB-12).</summary>
    public void Reset() => shared.ChangeTracker.Clear();

    private async Task JoinAsync(System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        if (shared.Database.CurrentTransaction?.GetDbTransaction() == transaction)
            return;
        if (shared.Database.CurrentTransaction is not null)
            await shared.Database.UseTransactionAsync(null, ct);
        if (shared.Database.GetDbConnection() != transaction.Connection)
            shared.Database.SetDbConnection(transaction.Connection, contextOwnsConnection: false);
        await shared.Database.UseTransactionAsync(transaction, ct);
    }
}
