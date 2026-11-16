using Clinical.Contracts;

namespace DocumentsReporting.Tests.Mocks;

public sealed class TestClinicalModule : IClinicalModule
{
    public Func<Guid, Guid, Task<bool>>? IsOriginalDoctorHandler { get; set; }
    public Func<Guid, Task<bool>>? IsPostClsConclusionPublishedHandler { get; set; }
    public Func<Guid, Task<ClsResultInfoDto?>>? GetClsResultHandler { get; set; }

    public Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct = default)
    {
        if (IsOriginalDoctorHandler != null)
            return IsOriginalDoctorHandler(doctorId, encounterId);

        return Task.FromResult(true);
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
