using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class Appointment
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid SessionQuotaId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    public Guid? RescheduledFromAppointmentId { get; set; }
    public string AppointmentNumber { get; set; } = null!;
    public decimal AppliedConsultationFee { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
