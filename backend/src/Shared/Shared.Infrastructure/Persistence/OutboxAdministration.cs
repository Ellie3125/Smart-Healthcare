using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

public sealed record OutboxException(Guid Id, Guid AggregateId, Guid? OperationId, string Status, DateTime CreatedAtUtc,
    int RetryCount, bool CanRetry);
public enum OutboxRetryResult { Retried, NotFound, NotDeadLetter }

public sealed class OutboxAdministration(SharedDbContext shared, AuditLogWriter audit, IDbClock clock)
{
    public async Task<IReadOnlyList<OutboxException>> ListExceptionsAsync(DateTime staleBeforeUtc, int take, CancellationToken ct) =>
        await shared.Set<OutboxMessage>().AsNoTracking().Where(x => x.Status == OutboxMessageStatus.DeadLetter
            || (x.Status == OutboxMessageStatus.Pending && x.RetryCount > 0)
            || (x.Status == OutboxMessageStatus.Processing && (x.ProcessedAtUtc ?? x.CreatedAtUtc) <= staleBeforeUtc))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(take)
            .Select(x => new OutboxException(x.Id, x.AggregateId, x.OperationId, x.Status.ToString(), x.CreatedAtUtc,
                x.RetryCount, x.Status == OutboxMessageStatus.DeadLetter)).ToListAsync(ct);

    public async Task<OutboxRetryResult> RetryAsync(Guid id, Guid userId, string note, CancellationToken ct)
    {
        shared.RequireTransaction("Retry Outbox");
        var entry = await shared.Set<OutboxMessage>().FromSql($"SELECT * FROM shared.OutboxMessage WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (entry is null) return OutboxRetryResult.NotFound;
        if (entry.Status != OutboxMessageStatus.DeadLetter) return OutboxRetryResult.NotDeadLetter;
        await shared.Set<OutboxMessage>().Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, OutboxMessageStatus.Pending).SetProperty(x => x.RetryCount, 0)
            .SetProperty(x => x.ProcessedAtUtc, (DateTime?)null), ct);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = userId, OperationId = entry.OperationId,
            TableName = "shared.OutboxMessage", RecordId = id, ActionType = ActionType.UPDATE,
            ChangedColumns = "Status,RetryCount,ProcessedAtUtc", ChangeReason = note,
            OldValuesJson = JsonSerializer.Serialize(new { Status = entry.Status.ToString(), entry.RetryCount, entry.ProcessedAtUtc }),
            NewValuesJson = """{"Status":"Pending","RetryCount":0,"ProcessedAtUtc":null}""",
            TimestampUtc = await clock.GetUtcNowAsync(ct) }, ct);
        return OutboxRetryResult.Retried;
    }
}
