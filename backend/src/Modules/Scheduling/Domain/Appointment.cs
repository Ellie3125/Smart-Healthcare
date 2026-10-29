namespace SmartHealthcare.Scheduling.Domain;

public sealed class Appointment
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid SessionQuotaId { get; set; }
    public DoctorSessionSlotQuota SessionQuota { get; set; } = null!;
    public Guid PatientId { get; set; }
    /// <summary>Lịch gốc khi đổi lịch (1B); mỗi lịch chỉ bị đổi một lần.</summary>
    public Guid? RescheduledFromAppointmentId { get; set; }
    public Appointment? RescheduledFromAppointment { get; set; }
    public string AppointmentNumber { get; set; } = null!;
    public decimal AppliedConsultationFee { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
