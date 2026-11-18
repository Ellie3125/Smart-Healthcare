namespace Notifications.Application.Exceptions;

public sealed record WorkflowExceptionItem(Guid Id, string Source, string Kind, string Status, Guid TargetEntityId,
    Guid? OperationId, DateTime CreatedAtUtc, int? RetryCount, bool CanRetryOutbox, DateTime? NextRetryAtUtc = null);
public sealed record WorkflowExceptionList(IReadOnlyList<WorkflowExceptionItem> Items, IReadOnlyList<string> UnavailableSources);
public enum RetryExceptionResult { Retried, InvalidInput, NotFound, Conflict, InProgress }
public interface IWorkflowExceptionMonitor
{
    Task<WorkflowExceptionList> GetAsync(int take, CancellationToken ct);
    Task<RetryExceptionResult> RetryOutboxAsync(Guid messageId, Guid userId, string note, string idempotencyKey, CancellationToken ct);
}
