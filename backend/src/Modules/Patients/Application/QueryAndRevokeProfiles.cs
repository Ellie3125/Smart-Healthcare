using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Application;

public sealed class ListMyPatientProfiles(IPatientProfileRepository profiles)
{
    public async Task<IReadOnlyList<PatientProfileDto>> ExecuteAsync(Guid appUserId, CancellationToken ct)
    {
        var accessible = await profiles.ListAccessibleAsync(appUserId, ct);
        return accessible.Select(p => PatientProfileDto.From(p.Patient, p.Link)).ToList();
    }
}

/// <summary>Hồ sơ không tồn tại và hồ sơ không thuộc quyền trả cùng một lỗi, để đổi ID trên URL không dò được hồ sơ người khác.</summary>
public sealed class GetPatientProfile(IPatientProfileRepository profiles)
{
    public async Task<Result<PatientProfileDto>> ExecuteAsync(Guid appUserId, Guid patientId, CancellationToken ct)
    {
        var profile = await profiles.FindAccessibleAsync(appUserId, patientId, ct);
        return profile is null ? ProfileErrors.NotFound : PatientProfileDto.From(profile.Patient, profile.Link);
    }
}

/// <summary>Tài khoản tự gỡ liên kết của mình với một hồ sơ. Hồ sơ vẫn giữ nguyên để không mất lịch sử khám.</summary>
public sealed class RevokeMyProfileLink(IPatientProfileRepository profiles, TimeProvider clock)
{
    public async Task<Result> ExecuteAsync(Guid appUserId, Guid patientId, CancellationToken ct)
    {
        var profile = await profiles.FindAccessibleAsync(appUserId, patientId, ct);
        if (profile is null) return ProfileErrors.NotFound;

        profile.Link.Revoke(clock.GetUtcDateTime());
        await profiles.SaveChangesAsync(ct);
        return Result.Success();
    }
}
