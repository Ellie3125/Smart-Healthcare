namespace SmartHealthcare.Workflow.Domain;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public Guid? RoomAccountId { get; set; }
    public Guid? RoomDeviceSessionId { get; set; }
    public Guid? OperationId { get; set; }
    public Operation? Operation { get; set; }
    public Guid? StaffShiftAssignmentId { get; set; }
    public string TableName { get; set; } = null!;
    public Guid RecordId { get; set; }
    public ActionType ActionType { get; set; }
    public string? DeviceId { get; set; }
    public string? ClientIpAddress { get; set; }
    public string? ChangedColumns { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime TimestampUtc { get; set; }
}
