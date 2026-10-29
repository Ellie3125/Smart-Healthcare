namespace SmartHealthcare.Workflow.Domain;

public sealed class BookingAttempt
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid PatientId { get; set; }
    public Guid? SelectedDoctorId { get; set; }
    public Guid? SelectedDoctorRankId { get; set; }
    public CurrentStep CurrentStep { get; set; }
    public DateOnly? SelectedServiceDate { get; set; }
    public BookingAttemptStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
