using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

/// <summary>Đọc event vừa ghi trong UoW để xử lý đồng bộ bằng đúng EventId; worker replay vẫn dùng ProcessedEvent.</summary>
public sealed class OutboxReader(SharedDbContext shared)
{
    public async Task<TEvent> ReadSingleAsync<TEvent>(Guid aggregateId, CancellationToken ct) where TEvent : IIntegrationEvent
    {
        shared.RequireTransaction("Đọc event trong transaction nghiệp vụ");
        var type = Enum.Parse<EventType>(typeof(TEvent).Name);
        var json = await shared.Set<OutboxMessage>().AsNoTracking()
            .Where(x => x.AggregateId == aggregateId && x.EventType == type).Select(x => x.PayloadJson).SingleAsync(ct);
        return JsonSerializer.Deserialize<TEvent>(json, OutboxWriter.JsonOptions)
            ?? throw new JsonException("Payload Outbox rỗng.");
    }
}
