namespace SmartHealthcare.Scheduling.Domain;

public sealed class Reservation
{
    public Guid Id { get; set; }
    public Guid SessionQuotaId { get; set; }
    public DoctorSessionSlotQuota SessionQuota { get; set; } = null!;
    public Guid BookingAttemptId { get; set; }
    public Guid PatientId { get; set; }
    public Guid AppUserId { get; set; }
    public BookingChannel BookingChannel { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public ReservationStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public byte[] RowVersion { get; set; } = null!;
}
