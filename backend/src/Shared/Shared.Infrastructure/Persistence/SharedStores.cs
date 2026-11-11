using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

internal static class TransactionGuard
{
    public static void RequireTransaction(this SharedDbContext shared, string what)
    {
        if (shared.Database.CurrentTransaction is null)
            throw new InvalidOperationException($"{what} phải chạy bên trong IUnitOfWork (transaction nghiệp vụ).");
    }
}

/// <summary>Ghi integration event vào Outbox của transaction UoW đang mở; EventType theo tên lớp sự kiện (MSG-02, MSG-03).</summary>
internal sealed class IntegrationEventPublisher(SharedDbContext shared, OutboxWriter outbox) : IIntegrationEventPublisher
{
    private static readonly Dictionary<EventType, AggregateType> Aggregates = new()
    {
        [EventType.PaymentVerified] = AggregateType.PaymentIntent,
        [EventType.TicketCheckedIn] = AggregateType.QueueTicket,
        [EventType.EncounterOpened] = AggregateType.Encounter,
        [EventType.ResultPublished] = AggregateType.CLSResult,
        [EventType.AppointmentConfirmed] = AggregateType.Appointment,
        [EventType.AppointmentCancelled] = AggregateType.Appointment,
    };

    public Task PublishAsync<TEvent>(TEvent e, Guid aggregateId, CancellationToken ct) where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(e);
        var name = e.GetType().Name;
        if (!Enum.TryParse<EventType>(name, out var type) || !Aggregates.TryGetValue(type, out var aggregate))
            throw new ArgumentException($"Sự kiện {name} không có trong danh sách EventType của Outbox.", nameof(e));

        return outbox.EnqueueAsync(shared, e, type, aggregate, aggregateId, ct);
    }
}

/// <summary>Đánh dấu (EventId, HandlerName) đã xử lý, cùng transaction với tác dụng của handler (MSG-05).</summary>
internal sealed class ProcessedEventStore(SharedDbContext shared, TimeProvider clock) : IProcessedEventStore
{
    public async Task<bool> TryMarkAsync(Guid eventId, string handlerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        shared.RequireTransaction("ProcessedEvent");

        // Khóa khoảng khóa unique để handler chạy song song cùng sự kiện chờ nhau thay vì cùng thấy "chưa có".
        var existing = await shared.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM shared.ProcessedEvent WITH (UPDLOCK, HOLDLOCK)
            WHERE EventId = {eventId} AND HandlerName = {handlerName}
            """).ToListAsync(ct);
        if (existing.Count > 0)
            return false;

        shared.Add(new ProcessedEvent { EventId = eventId, HandlerName = handlerName, ProcessedAtUtc = clock.GetUtcNow().UtcDateTime });
        await shared.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>Idempotency theo bộ ba khóa, khóa hàng UPDLOCK/HOLDLOCK để BE1 và BE2 tranh nhau tại DB (Giải pháp §4, DB-04).</summary>
internal sealed class IdempotencyStore(SharedDbContext shared, IDbClock dbClock) : IIdempotencyStore
{
    public async Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key, string requestHash,
        TimeSpan ttl, Guid? operationId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentOutOfRangeException.ThrowIfLessThan(ttl, TimeSpan.Zero);
        var record = await LockAsync(callerScope, operationType, key, ct);
        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa

        if (record is null)
        {
            record = new ApiIdempotencyRecord { CallerScope = callerScope, OperationType = operationType, IdempotencyKey = key };
            shared.Add(record);
        }
        else if (record.ExpiresAtUtc > now)
        {
            if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
                return new IdempotencyBeginResult.Conflict();
            return record.ResponseStatusCode is { } status
                ? new IdempotencyBeginResult.Replay(status, record.ResponseBodyJson, record.OperationId)
                : new IdempotencyBeginResult.InProgress(record.OperationId);
        }

        // Khóa mới, hoặc bản ghi đã hết hạn: ghi đè như khóa mới.
        record.RequestHash = requestHash;
        record.OperationId = operationId;
        record.ResponseStatusCode = null;
        record.ResponseBodyJson = null;
        record.CreatedAtUtc = now;
        record.ExpiresAtUtc = now + ttl;
        await shared.SaveChangesAsync(ct);
        return new IdempotencyBeginResult.Started();
    }

    public async Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson,
        Guid? operationId, CancellationToken ct)
    {
        var record = await LockAsync(callerScope, operationType, key, ct)
            ?? throw new InvalidOperationException("Chưa có bản ghi idempotency; gọi BeginAsync trước CompleteAsync.");
        record.ResponseStatusCode = statusCode;
        record.ResponseBodyJson = bodyJson;
        record.OperationId = operationId ?? record.OperationId;
        await shared.SaveChangesAsync(ct);
    }

    private async Task<ApiIdempotencyRecord?> LockAsync(string callerScope, string operationType, string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        shared.RequireTransaction("Idempotency");

        var rows = await shared.Set<ApiIdempotencyRecord>().FromSql($"""
            SELECT * FROM shared.ApiIdempotencyRecord WITH (UPDLOCK, HOLDLOCK)
            WHERE CallerScope = {callerScope} AND OperationType = {operationType} AND IdempotencyKey = {key}
            """).ToListAsync(ct);
        return rows.SingleOrDefault();
    }
}

/// <summary>Giờ UTC của SQL Server, đọc trên connection/transaction đang mở của SharedDbContext (Giải pháp §1.2).</summary>
internal sealed class DbClock(SharedDbContext shared) : IDbClock
{
    public async Task<DateTime> GetUtcNowAsync(CancellationToken ct)
    {
        var rows = await shared.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").ToListAsync(ct);
        return DateTime.SpecifyKind(rows.Single(), DateTimeKind.Utc);
    }
}

/// <summary>Ghi AuditLog; che NationalId và PhoneNumber trong giá trị trước/sau trước khi lưu.</summary>
public sealed class AuditLogWriter(SharedDbContext shared)
{
    public async Task WriteAsync(AuditLog entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.OldValuesJson = SensitiveDataMasker.MaskJson(entry.OldValuesJson);
        entry.NewValuesJson = SensitiveDataMasker.MaskJson(entry.NewValuesJson);
        shared.Add(entry);
        await shared.SaveChangesAsync(ct);
    }
}
