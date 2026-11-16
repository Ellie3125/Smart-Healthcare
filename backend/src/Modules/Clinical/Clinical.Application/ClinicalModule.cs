using Clinical.Contracts;

namespace Clinical.Application;

/// <summary>
/// Hiện thực tạm thời cho contract IClinicalModule trong lúc chờ PR của Mạnh hoàn thiện (Tuần 3).
/// Cung cấp kiểm tra quyền bác sĩ ban đầu và trạng thái kết luận sau CLS.
/// </summary>
public sealed class ClinicalModule : IClinicalModule
{
    // Có thể cấu hình hoặc mock trong quá trình kiểm thử
    public Func<Guid, Guid, Task<bool>>? IsOriginalDoctorHandler { get; set; }
    public Func<Guid, Task<bool>>? IsPostClsConclusionPublishedHandler { get; set; }
    public Func<Guid, Task<ClsResultInfoDto?>>? GetClsResultHandler { get; set; }

    public Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct = default)
    {
        if (IsOriginalDoctorHandler != null)
            return IsOriginalDoctorHandler(doctorId, encounterId);

        // Mặc định cho phép nếu doctorId và encounterId hợp lệ (đáp ứng test/demo khi chưa có store thật của Mạnh)
        return Task.FromResult(doctorId != Guid.Empty && encounterId != Guid.Empty);
    }

    public Task<bool> IsPostClsConclusionPublishedAsync(Guid encounterId, CancellationToken ct = default)
    {
        if (IsPostClsConclusionPublishedHandler != null)
            return IsPostClsConclusionPublishedHandler(encounterId);

        return Task.FromResult(true);
    }

    public Task<ClsResultInfoDto?> GetClsResultAsync(Guid clsResultId, CancellationToken ct = default)
    {
        if (GetClsResultHandler != null)
            return GetClsResultHandler(clsResultId);

        return Task.FromResult<ClsResultInfoDto?>(new ClsResultInfoDto(
            clsResultId,
            Guid.NewGuid(),
            "Published",
            Guid.NewGuid(),
            DateTime.UtcNow));
    }
}
