using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class ActiveBookingClaim
{
    public Guid Id { get; set; }
    public Guid SessionQuotaId { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid DoctorId { get; set; }
    public DateOnly ServiceDate { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid? AppointmentId { get; set; }
    public ClaimSource ClaimSource { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
}
