using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;

namespace Clinical.Application;

public sealed class ClinicalModule(
    IEncounterStore encounterStore,
    IServiceOrderStore serviceOrderStore,
    IPrescriptionStore prescriptionStore,
    TimeProvider clock,
    Func<Results.ClsLifecycleService>? clsLifecycle = null,
    Func<Encounters.EncounterProgressService>? encounterProgress = null, Reports.IClinicalReportReader? reports = null) : IClinicalModule
{
    public async Task<IReadOnlyList<EncounterDiagnosisDto>> GetPublishedDiagnosesAsync(Guid encounterId, CancellationToken ct = default)
    {
        if (encounterId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "EncounterId không được rỗng.");
        var record = await encounterStore.GetRecordByEncounterIdAsync(encounterId, ct);
        if (record is not { Status: EncounterClinicalRecordStatus.Published }) return [];
        return (await encounterStore.GetDiagnosesByEncounterIdAsync(encounterId, ct))
            .OrderBy(d => d.DiagnosedAtUtc).ThenBy(d => d.Id).Select(ClinicalMappers.ToDto).ToArray();
    }

    public Task<IReadOnlyList<ClinicalDailyReport>> GetDailyReportAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct = default) =>
        (reports ?? throw new NotSupportedException()).ReadAsync(fromUtc, untilUtc, ct);

    public Task StartReturnVisitAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct) =>
        (encounterProgress ?? throw new NotSupportedException("Host chưa đăng ký tiến trình lượt khám."))().StartReturnVisitAsync(encounterId, performedByUserId, ct);
    public Task FinishConsultationAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct) =>
        (encounterProgress ?? throw new NotSupportedException("Host chưa đăng ký tiến trình lượt khám."))().FinishConsultationAsync(encounterId, performedByUserId, ct);
    public Task StartClsServiceAsync(ClsServiceCommand command, CancellationToken ct) =>
        (clsLifecycle ?? throw new NotSupportedException("Host chưa đăng ký Clinical CLS lifecycle."))().TransitionAsync(command, true, ct);
    public Task FinishClsServiceAsync(ClsServiceCommand command, CancellationToken ct) =>
        (clsLifecycle ?? throw new NotSupportedException("Host chưa đăng ký Clinical CLS lifecycle."))().TransitionAsync(command, false, ct);

    public async Task<EncounterDto?> GetEncounterByIdAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "EncounterId không được rỗng.");

        var encounter = await encounterStore.GetByIdAsync(encounterId, ct);
        return encounter is null ? null : ClinicalMappers.ToDto(encounter);
    }

    public async Task<EncounterDto> StartEncounterAsync(StartEncounterCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.AppointmentId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "AppointmentId không được rỗng.");
        if (command.PatientId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "PatientId không được rỗng.");
        if (command.OriginalDoctorId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "OriginalDoctorId không được rỗng.");
        if (command.OriginalRoomId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "OriginalRoomId không được rỗng.");
        if (string.IsNullOrWhiteSpace(command.SpecialtySnapshot))
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "Chuyên khoa (SpecialtySnapshot) không được để trống.");
        if (string.IsNullOrWhiteSpace(command.DoctorRankSnapshot))
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "Cấp bậc bác sĩ (DoctorRankSnapshot) không được để trống.");

        var hasActive = await HasActiveEncounterAsync(command.PatientId, ct);
        if (hasActive)
            throw new ClinicalException(ClinicalErrorCode.ActiveEncounterAlreadyExists,
                $"Bệnh nhân {command.PatientId} hiện đang có một lượt khám chưa hoàn thành trong hệ thống.");

        var now = clock.GetUtcNow().UtcDateTime;
        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = command.AppointmentId,
            PatientId = command.PatientId,
            OriginalDoctorId = command.OriginalDoctorId,
            OriginalRoomId = command.OriginalRoomId,
            SpecialtySnapshot = command.SpecialtySnapshot.Trim(),
            DoctorRankSnapshot = command.DoctorRankSnapshot.Trim(),
            CheckedInAtUtc = now,
            Status = EncounterStatus.InProgress,
            CompletedAtUtc = null,
        };

        var initialRecord = new EncounterClinicalRecord
        {
            Id = Guid.NewGuid(),
            EncounterId = encounter.Id,
            Status = EncounterClinicalRecordStatus.Draft,
            RecordedAtUtc = now,
            LastUpdatedUtc = now,
            UpdatedByDoctorId = command.OriginalDoctorId,
        };

        encounterStore.AddEncounter(encounter);
        encounterStore.AddClinicalRecord(initialRecord);
        await encounterStore.SaveChangesAsync(ct);

        return ClinicalMappers.ToDto(encounter);
    }

    public async Task<bool> HasActiveEncounterAsync(Guid patientId, CancellationToken ct)
    {
        if (patientId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "PatientId không được rỗng.");

        var active = await encounterStore.GetActiveByPatientIdAsync(patientId, ct);
        return active is not null;
    }

    public async Task<IReadOnlyList<EncounterSummaryDto>> GetPatientEncountersAsync(Guid patientId, CancellationToken ct)
    {
        if (patientId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "PatientId không được rỗng.");

        var encounters = await encounterStore.GetByPatientIdAsync(patientId, ct);
        return encounters.Select(ClinicalMappers.ToSummaryDto).ToList();
    }

    public async Task<EncounterClinicalRecordDto?> GetClinicalRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "EncounterId không được rỗng.");

        var record = await encounterStore.GetRecordByEncounterIdAsync(encounterId, ct);
        return record is null ? null : ClinicalMappers.ToDto(record);
    }

    public async Task<IReadOnlyList<ServiceOrderDto>> GetServiceOrdersByEncounterIdAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "EncounterId không được rỗng.");

        var orders = await serviceOrderStore.GetByEncounterIdAsync(encounterId, ct);
        if (orders.Count == 0)
            return [];

        var resultList = new List<ServiceOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            var items = await serviceOrderStore.GetItemsByOrderIdAsync(order.Id, ct);
            var itemIds = items.Select(i => i.Id).ToList();
            var results = itemIds.Count == 0
                ? []
                : await serviceOrderStore.GetResultsByOrderItemIdsAsync(itemIds, ct);

            var resultMap = results.ToDictionary(r => r.ServiceOrderItemId);
            var itemDtos = items.Select(item =>
            {
                resultMap.TryGetValue(item.Id, out var res);
                return ClinicalMappers.ToDto(item, res);
            }).ToList();

            resultList.Add(new ServiceOrderDto(
                order.Id,
                order.EncounterId,
                order.OrderRoundNumber,
                order.OrderedAtUtc,
                order.PrescribedByDoctorId,
                order.OrderBatchStatus.ToString(),
                order.HasActiveReturnTicket,
                itemDtos));
        }

        return resultList;
    }

    public async Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct)
    {
        if (doctorId == Guid.Empty || encounterId == Guid.Empty)
            return false;

        var encounter = await encounterStore.GetByIdAsync(encounterId, ct);
        return encounter is not null && encounter.OriginalDoctorId == doctorId;
    }

    public async Task<bool> IsPostClsConclusionPublishedAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            return false;

        var record = await encounterStore.GetRecordByEncounterIdAsync(encounterId, ct);
        return record is { Status: EncounterClinicalRecordStatus.Published }
            && !string.IsNullOrWhiteSpace(record.PostCLSConclusion);
    }

    public async Task<IReadOnlyList<PrescriptionDto>> GetPrescriptionsByEncounterIdAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "EncounterId không được rỗng.");

        var prescriptions = await prescriptionStore.GetByEncounterIdAsync(encounterId, ct);
        if (prescriptions.Count == 0)
            return [];

        var prescriptionIds = prescriptions.Select(p => p.Id).ToList();
        var allItems = await prescriptionStore.GetItemsByPrescriptionIdsAsync(prescriptionIds, ct);
        var itemGroups = allItems.GroupBy(i => i.PrescriptionId).ToDictionary(g => g.Key, g => (IReadOnlyList<PrescriptionItem>)g.ToList());

        return prescriptions.Select(p =>
        {
            itemGroups.TryGetValue(p.Id, out var items);
            return ClinicalMappers.ToDto(p, items ?? []);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueuePatientContext>> GetQueuePatientContextsAsync(IReadOnlyCollection<Guid> encounterIds, CancellationToken ct = default)
    {
        if (encounterIds.Count > 100 || encounterIds.Contains(Guid.Empty))
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "Danh sách lượt khám không hợp lệ.");
        if (encounterIds.Count == 0) return [];
        return (await encounterStore.GetByIdsAsync(encounterIds, ct)).Select(e => new QueuePatientContext(e.Id, e.PatientId)).ToArray();
    }
}

