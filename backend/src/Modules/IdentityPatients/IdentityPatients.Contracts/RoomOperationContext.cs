namespace IdentityPatients.Contracts;

public sealed record RoomOperationContext(Guid AppUserId, Guid RoomAccountId, Guid RoomDeviceSessionId, Guid RoomId,
    string DeviceId, Guid StaffShiftAssignmentId, Guid ShiftLeaderStaffId, DateOnly ShiftDate, string ShiftName);
