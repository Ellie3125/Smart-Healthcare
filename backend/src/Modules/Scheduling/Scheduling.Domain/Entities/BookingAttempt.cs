using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class BookingAttempt
{
    public Guid Id { get; set; }
    // ID chéo module: identity.AppUser, không FK
    public Guid AppUserId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid? SelectedDoctorId { get; set; }
    // ID chéo module: identity.DoctorRank, không FK
    public Guid? SelectedDoctorRankId { get; set; }
    public CurrentStep CurrentStep { get; set; }
    public DateOnly SelectedServiceDate { get; set; }
    public BookingAttemptStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
