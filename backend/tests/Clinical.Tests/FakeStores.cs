using Clinical.Application;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;

namespace Clinical.Tests;

internal sealed class FakeEncounterStore : IEncounterStore
{
    public readonly List<Encounter> Encounters = [];
    public readonly List<EncounterClinicalRecord> Records = [];
    public readonly List<VitalSign> VitalSigns = [];
    public readonly List<EncounterDiagnosis> Diagnoses = [];

    private static readonly EncounterStatus[] ActiveStatuses =
    [
        EncounterStatus.InProgress,
        EncounterStatus.WaitingCLS,
        EncounterStatus.ReturningDoctor
    ];

    public Task<Encounter?> GetByIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult(Encounters.FirstOrDefault(e => e.Id == encounterId));

    public Task<Encounter?> GetActiveByPatientIdAsync(Guid patientId, CancellationToken ct) =>
        Task.FromResult(Encounters.FirstOrDefault(e => e.PatientId == patientId && ActiveStatuses.Contains(e.Status)));

    public Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Encounter>>(Encounters.Where(e => e.PatientId == patientId).ToList());

    public Task<EncounterClinicalRecord?> GetRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult(Records.FirstOrDefault(r => r.EncounterId == encounterId));

    public Task<IReadOnlyList<VitalSign>> GetVitalSignsByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VitalSign>>(VitalSigns.Where(v => v.EncounterId == encounterId).ToList());

    public Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EncounterDiagnosis>>(Diagnoses.Where(d => d.EncounterId == encounterId).ToList());

    public void AddEncounter(Encounter encounter) => Encounters.Add(encounter);

    public void AddClinicalRecord(EncounterClinicalRecord record) => Records.Add(record);

    public void AddVitalSign(VitalSign vitalSign) => VitalSigns.Add(vitalSign);

    public void AddDiagnosis(EncounterDiagnosis diagnosis) => Diagnoses.Add(diagnosis);

    public void RemoveDiagnosis(EncounterDiagnosis diagnosis) => Diagnoses.Remove(diagnosis);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakeServiceOrderStore : IServiceOrderStore
{
    public readonly List<ServiceOrder> Orders = [];
    public readonly List<ServiceOrderItem> Items = [];
    public readonly List<CLSResult> Results = [];

    public Task<IReadOnlyList<ServiceOrder>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ServiceOrder>>(Orders.Where(o => o.EncounterId == encounterId).ToList());

    public Task<ServiceOrder?> GetByIdAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Id == orderId));

    public Task<ServiceOrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId));

    public Task<IReadOnlyList<ServiceOrderItem>> GetItemsByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ServiceOrderItem>>(Items.Where(i => i.ServiceOrderId == orderId).ToList());

    public Task<CLSResult?> GetResultByItemIdAsync(Guid itemId, CancellationToken ct) =>
        Task.FromResult(Results.FirstOrDefault(r => r.ServiceOrderItemId == itemId));

    public Task<CLSResult?> GetResultByIdAsync(Guid resultId, CancellationToken ct) =>
        Task.FromResult(Results.FirstOrDefault(r => r.Id == resultId));

    public Task<IReadOnlyList<CLSResult>> GetResultsByOrderItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CLSResult>>(Results.Where(r => itemIds.Contains(r.ServiceOrderItemId)).ToList());

    public void AddOrder(ServiceOrder order) => Orders.Add(order);

    public void AddOrderItem(ServiceOrderItem item) => Items.Add(item);

    public void AddResult(CLSResult result) => Results.Add(result);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakePrescriptionStore : IPrescriptionStore
{
    public readonly List<Prescription> Prescriptions = [];
    public readonly List<PrescriptionItem> Items = [];

    public Task<IReadOnlyList<Prescription>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Prescription>>(Prescriptions.Where(p => p.EncounterId == encounterId).ToList());

    public Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct) =>
        Task.FromResult(Prescriptions.FirstOrDefault(p => p.Id == prescriptionId));

    public Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PrescriptionItem>>(Items.Where(i => i.PrescriptionId == prescriptionId).ToList());

    public Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdsAsync(IReadOnlyCollection<Guid> prescriptionIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PrescriptionItem>>(Items.Where(i => prescriptionIds.Contains(i.PrescriptionId)).ToList());

    public void AddPrescription(Prescription prescription) => Prescriptions.Add(prescription);

    public void AddItem(PrescriptionItem item) => Items.Add(item);

    public void RemoveItem(PrescriptionItem item) => Items.Remove(item);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

