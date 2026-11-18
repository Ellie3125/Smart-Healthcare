using IdentityPatients.Domain.Enums;

namespace IdentityPatients.Domain.Entities;

public sealed class PatientRepresentative
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid PatientId { get; set; }
    public Relationship Relationship { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
