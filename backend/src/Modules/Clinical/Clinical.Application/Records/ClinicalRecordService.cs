using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Shared.Abstractions;

namespace Clinical.Application.Records;

public sealed record DiagnosisInput(string? DiagnosisType, string? Icd10Code, string? DiagnosisName,
    bool IsPrimary = false, string? ClinicalNotes = null);
public sealed record ClinicalRecordRequest(byte[]? Version, IReadOnlyList<DiagnosisInput?>? Diagnoses = null,
    string? ChiefComplaint = null, string? Symptoms = null, string? MedicalHistory = null,
    string? ClinicalExaminationNotes = null, string? InitialClinicalImpression = null, string? PostCLSConclusion = null,
    string? TreatmentPlan = null, string? FollowUpInstructions = null, string? Reason = null);
public sealed record ClinicalRecordState(EncounterClinicalRecordDto Record, IReadOnlyList<EncounterDiagnosisDto> Diagnoses, string Version);
public enum RecordAction { Draft, Publish, Amend }
public enum RecordError { InvalidInput, Forbidden, EncounterNotFound, ClinicalRecordNotFound, EncounterClosed,
    AlreadyPublished, NotPublished, VersionConflict, IdempotencyConflict, RequestInProgress, HistoryUnavailable }
public sealed class ClinicalRecordException(RecordError error) : Exception(error.ToString())
{
    public RecordError Error { get; } = error;
}

public interface IClinicalRecordStore
{
    Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct);
    Task<EncounterClinicalRecord?> GetRecordAsync(Guid encounterId, CancellationToken ct);
    Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesAsync(Guid encounterId, CancellationToken ct);
    Task SaveAsync(EncounterClinicalRecord record, IReadOnlyList<EncounterDiagnosis> diagnoses, RecordAction action,
        string? reason, Guid actorUserId, CancellationToken ct);
}

public sealed class ClinicalRecordService(IClinicalRecordStore store, IUnitOfWork uow, IIdempotencyStore idempotency, IDbClock clock)
{
    private static readonly Regex Icd10 = new(@"^[A-Z][0-9]{2}(\.[0-9A-Z]{1,4})?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public Task<ClinicalRecordState> ReadAsync(Guid encounterId, Guid doctorId, CancellationToken ct) =>
        uow.ExecuteAsync(async t =>
        {
            await AuthorizeAsync(encounterId, doctorId, t);
            var record = await store.GetRecordAsync(encounterId, t) ?? throw new ClinicalRecordException(RecordError.ClinicalRecordNotFound);
            return State(record, await store.GetDiagnosesAsync(encounterId, t));
        }, ct);

    public Task<ClinicalRecordState> WriteAsync(Guid encounterId, Guid doctorId, Guid userId, RecordAction action,
        ClinicalRecordRequest request, string key, CancellationToken ct)
    {
        if (encounterId == Guid.Empty || doctorId == Guid.Empty || userId == Guid.Empty || request.Version is not { Length: 8 }
            || string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new ClinicalRecordException(RecordError.InvalidInput);
        var reason = request.Reason?.Trim();
        if (action == RecordAction.Amend && (string.IsNullOrWhiteSpace(reason) || reason.Length > EncounterClinicalRecordRevision.ReasonMaxLength))
            throw new ClinicalRecordException(RecordError.InvalidInput);
        var normalized = action == RecordAction.Publish ? null : Normalize(request.Diagnoses);
        request = request with { Diagnoses = normalized, Reason = reason };
        var caller = "AppUser:" + userId.ToString("D");
        var operation = "Clinical.Record." + action;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { encounterId, doctorId, request })));
        return uow.ExecuteAsync(async t =>
        {
            var encounter = await AuthorizeAsync(encounterId, doctorId, t);
            if (encounter.Status is EncounterStatus.Completed or EncounterStatus.Abandoned)
                throw new ClinicalRecordException(RecordError.EncounterClosed);
            var record = await store.GetRecordAsync(encounterId, t) ?? throw new ClinicalRecordException(RecordError.ClinicalRecordNotFound);
            // Published records reject draft/publish even when an old request key is replayed.
            if (action != RecordAction.Amend && record.Status == EncounterClinicalRecordStatus.Published)
                throw new ClinicalRecordException(RecordError.AlreadyPublished);
            if (action == RecordAction.Amend && record.Status != EncounterClinicalRecordStatus.Published)
                throw new ClinicalRecordException(RecordError.NotPublished);
            switch (await idempotency.BeginAsync(caller, operation, key, hash, TimeSpan.FromHours(24), null, t))
            {
                case IdempotencyBeginResult.Replay replay: return JsonSerializer.Deserialize<ClinicalRecordState>(replay.BodyJson!)!;
                case IdempotencyBeginResult.Conflict: throw new ClinicalRecordException(RecordError.IdempotencyConflict);
                case IdempotencyBeginResult.InProgress: throw new ClinicalRecordException(RecordError.RequestInProgress);
            }
            if (!record.RowVersion.AsSpan().SequenceEqual(request.Version))
                throw new ClinicalRecordException(RecordError.VersionConflict);
            var now = await clock.GetUtcNowAsync(t);
            IReadOnlyList<EncounterDiagnosis> diagnoses;
            if (normalized is null) diagnoses = await store.GetDiagnosesAsync(encounterId, t);
            else
            {
                record.ChiefComplaint = request.ChiefComplaint;
                record.Symptoms = request.Symptoms;
                record.MedicalHistory = request.MedicalHistory;
                record.ClinicalExaminationNotes = request.ClinicalExaminationNotes;
                record.InitialClinicalImpression = request.InitialClinicalImpression;
                record.PostCLSConclusion = request.PostCLSConclusion;
                record.TreatmentPlan = request.TreatmentPlan;
                record.FollowUpInstructions = request.FollowUpInstructions;
                diagnoses = normalized.Select(d => new EncounterDiagnosis { Id = Guid.CreateVersion7(), EncounterId = encounterId,
                    DiagnosisType = Enum.Parse<DiagnosisType>(d!.DiagnosisType!), ICD10Code = d.Icd10Code!, DiagnosisName = d.DiagnosisName!,
                    IsPrimary = d.IsPrimary, ClinicalNotes = d.ClinicalNotes ?? "", DiagnosedAtUtc = now }).ToArray();
            }
            record.LastUpdatedUtc = now;
            record.UpdatedByDoctorId = doctorId;
            if (action == RecordAction.Publish) { record.Status = EncounterClinicalRecordStatus.Published; record.PublishedAtUtc = now; }
            await store.SaveAsync(record, diagnoses, action, reason, userId, t);
            var result = State(record, diagnoses);
            await idempotency.CompleteAsync(caller, operation, key, 200, JsonSerializer.Serialize(result), null, t);
            return result;
        }, ct);
    }

    private async Task<Encounter> AuthorizeAsync(Guid encounterId, Guid doctorId, CancellationToken ct)
    {
        var encounter = await store.LockEncounterAsync(encounterId, ct) ?? throw new ClinicalRecordException(RecordError.EncounterNotFound);
        // Same guard as IsOriginalDoctorAsync, read after acquiring the encounter lock.
        if (doctorId == Guid.Empty || encounter.OriginalDoctorId != doctorId) throw new ClinicalRecordException(RecordError.Forbidden);
        return encounter;
    }

    private static IReadOnlyList<DiagnosisInput?> Normalize(IReadOnlyList<DiagnosisInput?>? diagnoses)
    {
        if (diagnoses is null || diagnoses.Count > 100) throw new ClinicalRecordException(RecordError.InvalidInput);
        return diagnoses.Select(d =>
        {
            var code = d?.Icd10Code?.Trim().ToUpperInvariant();
            var name = d?.DiagnosisName?.Trim();
            if (d is null || d.DiagnosisType is not ("Initial" or "Final") || code is null || !Icd10.IsMatch(code)
                || string.IsNullOrWhiteSpace(name) || name.Length > 200)
                throw new ClinicalRecordException(RecordError.InvalidInput);
            return d with { Icd10Code = code, DiagnosisName = name };
        }).ToArray();
    }

    private static ClinicalRecordState State(EncounterClinicalRecord record, IReadOnlyList<EncounterDiagnosis> diagnoses) =>
        new(ClinicalMappers.ToDto(record), diagnoses.Select(ClinicalMappers.ToDto).ToArray(), Convert.ToBase64String(record.RowVersion));
}
