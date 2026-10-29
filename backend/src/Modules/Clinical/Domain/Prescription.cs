namespace SmartHealthcare.Clinical.Domain;

public sealed class Prescription
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public Guid PrescribedByDoctorId { get; set; }
    public string PrescriptionCode { get; set; } = null!;
    public PrescriptionStatus Status { get; set; }
    public string? GeneralInstructions { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
