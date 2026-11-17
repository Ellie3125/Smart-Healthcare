using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Contracts;
using Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Exceptions;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Exceptions;

public sealed class WorkflowExceptionMonitor(WorkflowDbContext workflow, OutboxAdministration outbox, IDbClock clock,
    IUnitOfWork uow, IIdempotencyStore idempotency, IBillingStatusReader? billing = null,
    ICoordinationExceptionReader? coordination = null) : IWorkflowExceptionMonitor
{
    private const string RetryOperation = "Workflow.RetryOutbox";

    public async Task<WorkflowExceptionList> GetAsync(int take, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 100);
        var cutoff = (await clock.GetUtcNowAsync(ct)).AddMinutes(-5);
        var operations = await workflow.Set<Operation>().AsNoTracking()
            // State the filtered-index predicate explicitly; SQL Server does not infer it from the exception branches.
            .Where(x => x.Status == OperationStatus.Pending || x.Status == OperationStatus.Running
                || x.Status == OperationStatus.WaitingExternal || x.Status == OperationStatus.Failed
                || x.Status == OperationStatus.NeedsAction || x.Status == OperationStatus.ManualReview)
            .Where(x => x.Status == OperationStatus.Failed
            || x.Status == OperationStatus.NeedsAction || x.Status == OperationStatus.ManualReview
            || ((x.Status == OperationStatus.Pending || x.Status == OperationStatus.Running || x.Status == OperationStatus.WaitingExternal)
                && x.CreatedAtUtc <= cutoff))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(take)
            .Select(x => new WorkflowExceptionItem(x.Id, "Operation",
                x.Status == OperationStatus.Pending || x.Status == OperationStatus.Running || x.Status == OperationStatus.WaitingExternal
                    ? "OperationStalled" : "OperationNeedsReview", x.Status.ToString(), x.TargetEntityId, x.Id, x.CreatedAtUtc, null, false))
            .ToListAsync(ct);
        var messages = await outbox.ListExceptionsAsync(cutoff, take, ct);
        var unavailable = new List<string>();
        IReadOnlyList<PaidUnallocatedItem> refunds = [];
        IReadOnlyList<CoordinationExceptionView> tickets = [];
        if (billing is null) unavailable.Add("Billing.PaidUnallocated");
        else refunds = (await billing.GetPaidUnallocatedAsync(Guid.Empty, take, ct)).Items;
        if (coordination is null) unavailable.Add("Scheduling.Coordination");
        else tickets = await coordination.ListAsync(take, ct);
        return new(operations.Concat(messages.Select(x => new WorkflowExceptionItem(x.Id, "Outbox", "OutboxDelivery",
                x.Status, x.AggregateId, x.OperationId, x.CreatedAtUtc, x.RetryCount, x.CanRetry, x.NextRetryAtUtc)))
            .Concat(refunds.Select(x => new WorkflowExceptionItem(x.RefundRequestId, "Billing", "PaidUnallocated", x.Status,
                x.PaymentIntentId, null, x.RequestedAtUtc, null, false)))
            .Concat(tickets.Select(x => new WorkflowExceptionItem(x.QueueTicketId, "Scheduling", x.Reason, x.QueueStatus,
                x.EncounterId, null, x.QueuedAtUtc, null, false)))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Source, StringComparer.Ordinal).ThenBy(x => x.Id).Take(take).ToArray(),
            unavailable);
    }

    public Task<RetryExceptionResult> RetryOutboxAsync(Guid messageId, Guid userId, string note, string idempotencyKey, CancellationToken ct)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(note) || note.Length > 500
            || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128
            || idempotencyKey.Any(c => c < '!' || c > '~'))
            return Task.FromResult(RetryExceptionResult.InvalidInput);
        var caller = "AppUser:" + userId.ToString("D");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { messageId, note }))));
        return uow.ExecuteAsync(async t =>
        {
            switch (await idempotency.BeginAsync(caller, RetryOperation, idempotencyKey, hash, TimeSpan.FromHours(24), null, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return JsonSerializer.Deserialize<RetryExceptionResult>(replay.BodyJson!);
                case IdempotencyBeginResult.Conflict: return RetryExceptionResult.Conflict;
                case IdempotencyBeginResult.InProgress: return RetryExceptionResult.InProgress;
            }
            var outcome = await outbox.RetryAsync(messageId, userId, note, t) switch
            {
                OutboxRetryResult.Retried => RetryExceptionResult.Retried,
                OutboxRetryResult.NotFound => RetryExceptionResult.NotFound,
                OutboxRetryResult.NotDeadLetter => RetryExceptionResult.Conflict,
                _ => throw new InvalidOperationException("Unexpected retry outcome.")
            };
            await idempotency.CompleteAsync(caller, RetryOperation, idempotencyKey,
                outcome == RetryExceptionResult.Retried ? 200 : outcome == RetryExceptionResult.NotFound ? 404 : 409,
                JsonSerializer.Serialize(outcome), null, t);
            return outcome;
        }, ct);
    }
}
