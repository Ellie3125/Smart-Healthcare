namespace SmartHealthcare.Workflow.Domain;

public sealed class Operation
{
    public Guid Id { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public Guid? BookingAttemptId { get; set; }
    public BookingAttempt? BookingAttempt { get; set; }
    public OperationType OperationType { get; set; }
    public TargetEntityType TargetEntityType { get; set; }
    public Guid TargetEntityId { get; set; }
    public OperationStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
