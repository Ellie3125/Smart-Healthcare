using IdentityPatients.Domain.Enums;

namespace IdentityPatients.Domain.Entities;

public sealed class StaffShiftAssignment
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid RoomId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public ShiftName ShiftName { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsShiftLeader { get; set; }
}
