namespace SmartHealthcare.Identity.Domain;

public sealed class PatientRepresentative
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public Relationship Relationship { get; set; }
    public string? AuthorizationProofDoc { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
