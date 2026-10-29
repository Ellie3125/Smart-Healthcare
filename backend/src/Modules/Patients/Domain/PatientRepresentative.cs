namespace SmartHealthcare.Patients.Domain;

public enum Relationship
{
    Self,
    Child,
    Parent,
    Spouse,
    Other,
}

public enum VerificationStatus
{
    Pending,
    Verified,
    Rejected,
    Revoked,
}

/// <summary>Tài khoản nào được thao tác cho hồ sơ nào (bảng dbo.PatientRepresentative).</summary>
public sealed class PatientRepresentative
{
    private PatientRepresentative()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>ID của dbo.AppUser (module Identity). Database có FK tới AppUser, nhưng code Patients không đọc bảng đó (MOD-01).</summary>
    public Guid AppUserId { get; private set; }

    public Guid PatientId { get; private set; }
    public Relationship Relationship { get; private set; }
    public string? AuthorizationProofDoc { get; private set; }
    public VerificationStatus VerificationStatus { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool GrantsAccess => IsActive && VerificationStatus == VerificationStatus.Verified;

    /// <summary>Tài khoản tự tạo hồ sơ thì được xác minh ngay với hồ sơ đó. Liên kết vào hồ sơ có sẵn (Pending) thuộc luồng tiếp đón, ngoài FR01.</summary>
    public static PatientRepresentative ForProfileCreator(Guid appUserId, Guid patientId, Relationship relationship, DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AppUserId = appUserId,
            PatientId = patientId,
            Relationship = relationship,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = nowUtc,
        };

    public void Revoke(DateTime nowUtc)
    {
        IsActive = false;
        VerificationStatus = VerificationStatus.Revoked;
        RevokedAtUtc = nowUtc;
    }
}
