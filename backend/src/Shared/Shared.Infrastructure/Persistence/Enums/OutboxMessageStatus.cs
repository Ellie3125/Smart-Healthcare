namespace Shared.Infrastructure.Persistence.Enums;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Published,
    DeadLetter
}
