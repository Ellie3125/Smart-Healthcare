using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Domain.ValueObjects;
using Shared.Abstractions;

namespace Clinical.Application.Vitals;

public sealed record RecordVitalsRequest(decimal? WeightKg = null, decimal? HeightCm = null,
    decimal? SystolicBp = null, decimal? DiastolicBp = null, int? HeartRate = null,
    decimal? TemperatureC = null, decimal? Spo2 = null, int? RespiratoryRate = null, string? NurseNote = null);
public sealed record RecordedVitals(VitalSignDto VitalSign, decimal? Bmi);
public enum VitalsError { InvalidInput, EncounterNotFound, EncounterClosed, IdempotencyConflict, RequestInProgress }
public sealed class VitalsException(VitalsError error) : Exception(error.ToString())
{
    public VitalsError Error { get; } = error;
}

public interface IVitalsStore
{
    Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct);
    Task AppendAsync(VitalSign vitalSign, Guid actorUserId, CancellationToken ct);
}

/// <summary>Returns a verified active Nurse's Staff.Id; null denies the write. Never use AppUser.Id as Staff.Id.</summary>
public interface INurseStaffDirectory
{
    Task<Guid?> GetActiveNurseStaffIdAsync(Guid appUserId, CancellationToken ct);
}

public sealed class RecordVitalsService(IVitalsStore store, IUnitOfWork uow, IIdempotencyStore idempotency, IDbClock clock)
{
    public const string Operation = "Clinical.RecordVitals";

    public Task<RecordedVitals> RecordAsync(Guid encounterId, Guid staffId, Guid userId, RecordVitalsRequest request,
        string key, CancellationToken ct)
    {
        if (encounterId == Guid.Empty || staffId == Guid.Empty || userId == Guid.Empty
            || string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new VitalsException(VitalsError.InvalidInput);
        try
        {
            VitalSignsValidator.Validate(request.WeightKg, request.HeightCm, request.SystolicBp, request.DiastolicBp,
                request.HeartRate, request.TemperatureC, request.Spo2, request.RespiratoryRate, request.NurseNote);
            Precision(request.WeightKg, 2); Precision(request.HeightCm, 1); Precision(request.SystolicBp, 2);
            Precision(request.DiastolicBp, 2); Precision(request.TemperatureC, 1); Precision(request.Spo2, 2);
        }
        catch (ArgumentException) { throw new VitalsException(VitalsError.InvalidInput); }
        BloodPressure? pressure = request.SystolicBp is { } systolic && request.DiastolicBp is { } diastolic
            ? new BloodPressure(systolic, diastolic) : null;
        BmiResult? bmi = request.WeightKg is { } weight && request.HeightCm is { } height
            ? BmiCalculator.Calculate(weight, height) : null;
        var caller = "AppUser:" + userId.ToString("D");
        // Numeric formatting must not make an equivalent measurement conflict (70 and 70.00).
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { encounterId,
            WeightKg = Number(request.WeightKg), HeightCm = Number(request.HeightCm),
            SystolicBp = Number(request.SystolicBp), DiastolicBp = Number(request.DiastolicBp),
            request.HeartRate, TemperatureC = Number(request.TemperatureC), Spo2 = Number(request.Spo2),
            request.RespiratoryRate, request.NurseNote })));
        return uow.ExecuteAsync(async t =>
        {
            switch (await idempotency.BeginAsync(caller, Operation, key, hash, TimeSpan.FromHours(24), null, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return JsonSerializer.Deserialize<RecordedVitals>(replay.BodyJson!)!;
                case IdempotencyBeginResult.Conflict: throw new VitalsException(VitalsError.IdempotencyConflict);
                case IdempotencyBeginResult.InProgress: throw new VitalsException(VitalsError.RequestInProgress);
            }
            var encounter = await store.LockEncounterAsync(encounterId, t)
                ?? throw new VitalsException(VitalsError.EncounterNotFound);
            if (encounter.Status is EncounterStatus.Completed or EncounterStatus.Abandoned)
                throw new VitalsException(VitalsError.EncounterClosed);
            var vital = new VitalSign { Id = Guid.CreateVersion7(), EncounterId = encounterId,
                RecordedByStaffId = staffId, RecordedAtUtc = await clock.GetUtcNowAsync(t),
                WeightKg = request.WeightKg, HeightCm = request.HeightCm,
                SystolicBP = pressure?.Systolic ?? request.SystolicBp, DiastolicBP = pressure?.Diastolic ?? request.DiastolicBp,
                PulseRate = request.HeartRate, BodyTemperature = request.TemperatureC, SpO2 = request.Spo2,
                RespiratoryRate = request.RespiratoryRate, NurseNote = request.NurseNote };
            await store.AppendAsync(vital, userId, t);
            var result = new RecordedVitals(ClinicalMappers.ToDto(vital), bmi?.Value);
            await idempotency.CompleteAsync(caller, Operation, key, 201, JsonSerializer.Serialize(result), null, t);
            return result;
        }, ct);
    }

    private static string? Number(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);

    private static void Precision(decimal? value, int scale)
    {
        if (value is { } number && decimal.Round(number, scale) != number)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
