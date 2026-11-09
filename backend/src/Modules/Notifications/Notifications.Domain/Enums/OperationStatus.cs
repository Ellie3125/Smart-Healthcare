namespace Notifications.Domain.Enums;

public enum OperationStatus
{
    Pending,
    Running,
    WaitingExternal,
    Succeeded,
    Failed,
    NeedsAction,
    ManualReview
}
