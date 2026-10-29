using SmartHealthcare.Patients.Domain;

namespace SmartHealthcare.Patients.Application;

/// <summary>Một hồ sơ cùng liên kết đại diện đang cho phép tài khoản truy cập.</summary>
public sealed record AccessibleProfile(Patient Patient, PatientRepresentative Link);

public enum AddProfileOutcome
{
    Added,
    DuplicateNationalId,
    SelfProfileExists,
}

public enum SaveProfileOutcome
{
    Saved,
    VersionConflict,
    DuplicateNationalId,
}

public interface IPatientProfileRepository
{
    Task<bool> HasActiveSelfLinkAsync(Guid appUserId, CancellationToken ct);

    /// <summary>Lưu hồ sơ và liên kết trong cùng một lần ghi; unique index quyết định khi hai request chạy song song.</summary>
    Task<AddProfileOutcome> TryAddAsync(Patient patient, PatientRepresentative link, CancellationToken ct);

    /// <summary>Chỉ trả hồ sơ có liên kết đang hoạt động và đã xác minh với tài khoản này.</summary>
    Task<IReadOnlyList<AccessibleProfile>> ListAccessibleAsync(Guid appUserId, CancellationToken ct);

    Task<AccessibleProfile?> FindAccessibleAsync(Guid appUserId, Guid patientId, CancellationToken ct);

    /// <summary>Ghi thay đổi hồ sơ nếu phiên bản trong DB vẫn bằng phiên bản client đã đọc.</summary>
    Task<SaveProfileOutcome> SaveProfileAsync(Patient patient, byte[] expectedVersion, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
