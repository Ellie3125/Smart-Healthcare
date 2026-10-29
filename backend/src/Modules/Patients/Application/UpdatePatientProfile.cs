using SmartHealthcare.Patients.Domain;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Application;

public sealed record UpdatePatientProfileCommand(
    Guid AppUserId,
    Guid PatientId,
    string? Version,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? NationalId,
    string? PhoneNumber,
    string? Address);

/// <summary>Sửa hồ sơ bằng khóa lạc quan: phiên bản đã đổi thì báo xung đột, không ghi đè.</summary>
public sealed class UpdatePatientProfile(IPatientProfileRepository profiles, TimeProvider clock)
{
    public async Task<Result<PatientProfileDto>> ExecuteAsync(UpdatePatientProfileCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var expectedVersion = TryDecodeVersion(command.Version);
        if (expectedVersion is null) errors["version"] = ["Thiếu hoặc sai phiên bản hồ sơ; tải lại hồ sơ rồi sửa."];

        var details = PatientDetails.Create(
            command.FullName, command.DateOfBirth, command.Gender, command.NationalId, command.PhoneNumber, command.Address,
            VietnamTime.Today(clock));
        if (!details.IsSuccess)
            foreach (var (field, messages) in details.Error.Fields!) errors[field] = messages;
        if (errors.Count > 0) return Error.Validation(errors);

        var profile = await profiles.FindAccessibleAsync(command.AppUserId, command.PatientId, ct);
        if (profile is null) return ProfileErrors.NotFound;

        var update = profile.Patient.Update(details.Value!);
        if (!update.IsSuccess) return update.Error;

        return await profiles.SaveProfileAsync(profile.Patient, expectedVersion!, ct) switch
        {
            SaveProfileOutcome.Saved => PatientProfileDto.From(profile.Patient, profile.Link),
            SaveProfileOutcome.VersionConflict => Error.Conflict(
                "profile_version_conflict", "Hồ sơ vừa được cập nhật ở nơi khác; tải lại để xem bản mới nhất."),
            _ => ProfileErrors.DuplicateNationalId,
        };
    }

    private static byte[]? TryDecodeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var buffer = new byte[8];
        return Convert.TryFromBase64String(version, buffer, out var written) && written == 8 ? buffer : null;
    }
}
