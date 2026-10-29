namespace SmartHealthcare.Clinical.Domain;

public sealed class Encounter
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid OriginalDoctorId { get; set; }
    public Guid OriginalRoomId { get; set; }
    public string SpecialtySnapshot { get; set; } = null!;
    public string DoctorRankSnapshot { get; set; } = null!;
    public DateTime CheckedInAtUtc { get; set; }
    public EncounterStatus Status { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
