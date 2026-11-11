using Clinical.Domain.Entities;

namespace Clinical.Application;

public interface IEncounterStore
{
    Task<Encounter?> GetByIdAsync(Guid encounterId, CancellationToken ct);

    Task<Encounter?> GetActiveByPatientIdAsync(Guid patientId, CancellationToken ct);

    Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct);

    Task<EncounterClinicalRecord?> GetRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    Task<IReadOnlyList<VitalSign>> GetVitalSignsByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    void AddEncounter(Encounter encounter);

    void AddClinicalRecord(EncounterClinicalRecord record);

    void AddVitalSign(VitalSign vitalSign);

    void AddDiagnosis(EncounterDiagnosis diagnosis);

    void RemoveDiagnosis(EncounterDiagnosis diagnosis);

    Task SaveChangesAsync(CancellationToken ct);
}

public interface IServiceOrderStore
{
    Task<IReadOnlyList<ServiceOrder>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    Task<ServiceOrder?> GetByIdAsync(Guid orderId, CancellationToken ct);

    Task<ServiceOrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct);

    Task<IReadOnlyList<ServiceOrderItem>> GetItemsByOrderIdAsync(Guid orderId, CancellationToken ct);

    Task<CLSResult?> GetResultByItemIdAsync(Guid itemId, CancellationToken ct);

    Task<CLSResult?> GetResultByIdAsync(Guid resultId, CancellationToken ct);

    Task<IReadOnlyList<CLSResult>> GetResultsByOrderItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    void AddOrder(ServiceOrder order);

    void AddOrderItem(ServiceOrderItem item);

    void AddResult(CLSResult result);

    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPrescriptionStore
{
    Task<IReadOnlyList<Prescription>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct);

    Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct);

    Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdsAsync(IReadOnlyCollection<Guid> prescriptionIds, CancellationToken ct);

    void AddPrescription(Prescription prescription);

    void AddItem(PrescriptionItem item);

    void RemoveItem(PrescriptionItem item);

    Task SaveChangesAsync(CancellationToken ct);
}

