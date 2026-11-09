using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class DoctorDayQuota
{
    public Guid Id { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid DoctorId { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid AssignedRoomId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public int Capacity { get; set; }
    public int HeldCount { get; set; }
    public int AdmittedCount { get; set; }
    public int LockedCancelledCount { get; set; }
    public DateTime SessionStartTimeUtc { get; set; }
    public DateTime BookingCutoffTimeUtc { get; set; }
    public DateTime RescheduleCutoffTimeUtc { get; set; }
    public DateTime? CheckInDeadlineUtc { get; set; }
    public SessionStatus SessionStatus { get; set; }
    public bool IsOpen { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
