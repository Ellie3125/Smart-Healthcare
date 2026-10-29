namespace SmartHealthcare.Scheduling.Domain;

public sealed class ActiveBookingClaim
{
    public Guid Id { get; set; }
    public Guid SessionQuotaId { get; set; }
    public DoctorSessionSlotQuota SessionQuota { get; set; } = null!;
    public Guid DoctorId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public Guid PatientId { get; set; }
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public ClaimSource ClaimSource { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
}
