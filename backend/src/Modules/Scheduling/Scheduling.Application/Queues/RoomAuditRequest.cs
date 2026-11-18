namespace Scheduling.Application.Queues;

// Populated only from signed claims by the API; not a body DTO or a record that prints its token.
public sealed class RoomAuditRequest
{
    public bool IsRoomAccount { get; set; }
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public string? SessionToken { get; set; }
    public string? ClientIpAddress { get; set; }
    public Guid? OperationId { get; set; }
}
public sealed class RoomAuditDeniedException : Exception;
