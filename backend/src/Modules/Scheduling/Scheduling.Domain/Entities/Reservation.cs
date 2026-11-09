using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class Reservation
{
    public Guid Id { get; set; }
    public Guid SessionQuotaId { get; set; }
    public Guid BookingAttemptId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    // ID chéo module: identity.AspNetUsers, không FK
    public Guid AppUserId { get; set; }
    public BookingChannel BookingChannel { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public ReservationStatus Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
