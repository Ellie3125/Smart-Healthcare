using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

public sealed class Prescription
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid PrescribedByDoctorId { get; set; }
    public string PrescriptionCode { get; set; } = null!;
    public PrescriptionStatus Status { get; set; }
    public string GeneralInstructions { get; set; } = null!;
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
