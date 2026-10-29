namespace SmartHealthcare.Identity.Domain;

public sealed class StaffShiftAssignment
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DateOnly ShiftDate { get; set; }
    public ShiftName ShiftName { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsShiftLeader { get; set; }
}
