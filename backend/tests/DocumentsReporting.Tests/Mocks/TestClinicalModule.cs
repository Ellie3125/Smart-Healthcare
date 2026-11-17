using Clinical.Contracts;

namespace DocumentsReporting.Tests.Mocks;

// Documents chỉ gọi hai hàm kiểm quyền; các hàm còn lại của IClinicalModule không dùng trong test này.
public sealed class TestClinicalModule : IClinicalModule
{
    public Func<Guid, Guid, Task<bool>>? IsOriginalDoctorHandler { get; set; }
    public Func<Guid, Task<bool>>? IsPostClsConclusionPublishedHandler { get; set; }

    public Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct) =>
        IsOriginalDoctorHandler?.Invoke(doctorId, encounterId) ?? Task.FromResult(true);

    public Task<bool> IsPostClsConclusionPublishedAsync(Guid encounterId, CancellationToken ct) =>
        IsPostClsConclusionPublishedHandler?.Invoke(encounterId) ?? Task.FromResult(true);

    public Task<EncounterDto?> GetEncounterByIdAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
    public Task<EncounterDto> StartEncounterAsync(StartEncounterCommand command, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasActiveEncounterAsync(Guid patientId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<EncounterSummaryDto>> GetPatientEncountersAsync(Guid patientId, CancellationToken ct) => throw new NotSupportedException();
    public Task<EncounterClinicalRecordDto?> GetClinicalRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ServiceOrderDto>> GetServiceOrdersByEncounterIdAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<PrescriptionDto>> GetPrescriptionsByEncounterIdAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
}
