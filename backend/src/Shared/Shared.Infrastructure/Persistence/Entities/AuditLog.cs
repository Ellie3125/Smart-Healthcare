using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    // ID chéo module: identity.AppUser, không FK
    public Guid? PerformedByUserId { get; set; }
    // ID chéo module: identity.RoomAccount, không FK
    public Guid? RoomAccountId { get; set; }
    // ID chéo module: identity.RoomDeviceSession, không FK
    public Guid? RoomDeviceSessionId { get; set; }
    // ID chéo module: workflow.Operation, không FK
    public Guid? OperationId { get; set; }
    // ID chéo module: identity.StaffShiftAssignment, không FK
    public Guid? StaffShiftAssignmentId { get; set; }
    public string TableName { get; set; } = null!;
    public Guid RecordId { get; set; }
    public ActionType ActionType { get; set; }
    public string? DeviceId { get; set; }
    public string? ClientIpAddress { get; set; }
    public string ChangedColumns { get; set; } = null!;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime TimestampUtc { get; set; }
}
