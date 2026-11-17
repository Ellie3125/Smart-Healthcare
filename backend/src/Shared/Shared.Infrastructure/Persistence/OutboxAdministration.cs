using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Abstractions;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

public sealed record OutboxException(Guid Id, Guid AggregateId, Guid? OperationId, string Status, DateTime CreatedAtUtc,
    int RetryCount, bool CanRetry, DateTime? NextRetryAtUtc);
public enum OutboxRetryResult { Retried, NotFound, NotDeadLetter }

public sealed class OutboxAdministration(SharedDbContext shared, AuditLogWriter audit, IDbClock clock, IConfiguration? configuration = null)
{
    private readonly OutboxOptions _options = new() { RetryDelaySeconds = int.Parse(
        configuration?[$"{OutboxOptions.Section}:RetryDelaySeconds"] ?? "30", CultureInfo.InvariantCulture) };

    public async Task<IReadOnlyList<OutboxException>> ListExceptionsAsync(DateTime staleBeforeUtc, int take, CancellationToken ct)
    {
        _options.Validate();
        var messages = await shared.Set<OutboxMessage>().AsNoTracking()
            .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Processing || x.Status == OutboxMessageStatus.DeadLetter)
            .Where(x => x.Status == OutboxMessageStatus.DeadLetter
            || (x.Status == OutboxMessageStatus.Pending && x.RetryCount > 0)
            || (x.Status == OutboxMessageStatus.Processing && (x.ProcessedAtUtc ?? x.CreatedAtUtc) <= staleBeforeUtc))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(take)
            .Select(x => new { x.Id, x.AggregateId, x.OperationId, x.Status, x.CreatedAtUtc, x.RetryCount, x.ProcessedAtUtc }).ToListAsync(ct);
        return messages.Select(x => new OutboxException(x.Id, x.AggregateId, x.OperationId, x.Status.ToString(), x.CreatedAtUtc,
            x.RetryCount, x.Status == OutboxMessageStatus.DeadLetter,
            x.Status == OutboxMessageStatus.Pending && x.ProcessedAtUtc is { } lastFailure
                // Match the dispatcher's existing exponent limit for unusually large retry counts.
                ? lastFailure.AddSeconds(_options.RetryDelaySeconds * Math.Pow(2, Math.Min(x.RetryCount, 10) - 1)) : null)).ToArray();
    }

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
