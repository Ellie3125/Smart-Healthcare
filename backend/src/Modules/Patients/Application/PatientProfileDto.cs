using SmartHealthcare.Patients.Domain;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Application;

/// <summary>Hồ sơ trả cho client. CCCD chỉ hiện 4 số cuối; Version dùng khi gửi lệnh sửa.</summary>
public sealed record PatientProfileDto(
    Guid Id,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string? NationalIdMasked,
    string? PhoneNumber,
    string? Address,
    string Relationship,
    string Version,
    DateTime CreatedAtUtc)
{
    public static PatientProfileDto From(Patient patient, PatientRepresentative link) =>
        new(
            patient.Id,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender.ToString(),
            MaskNationalId(patient.NationalId),
            patient.PhoneNumber,
            patient.Address,
            link.Relationship.ToString(),
            Convert.ToBase64String(patient.RowVersion),
            patient.CreatedAtUtc);

    public static string? MaskNationalId(string? nationalId) =>
        nationalId is { Length: > 4 } ? new string('*', nationalId.Length - 4) + nationalId[^4..] : nationalId;
}

internal static class ProfileErrors
{
    public static readonly Error NotFound = Error.NotFound("patient_profile_not_found", "Không tìm thấy hồ sơ.");

    public static readonly Error DuplicateNationalId = Error.Conflict(
        "national_id_already_registered", "CCCD này đã có hồ sơ trong hệ thống; liên hệ quầy tiếp đón để được liên kết.");
}
