namespace Scheduling.Domain.Enums;

public enum QueueStatus
{
    Waiting,
    Called,
    Deferred,
    InProgress,
    Finished,
    Cancelled
}
