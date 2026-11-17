using Clinical.Application.Vitals;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Shared.Abstractions;
using Xunit;

namespace Clinical.Tests;

public sealed class RecordVitalsServiceTests
{
    public static IEnumerable<object[]> InvalidMeasurements()
    {
        var empty = new RecordVitalsRequest();
        foreach (var value in new[] {
            empty with { WeightKg = .49m }, empty with { WeightKg = 501 }, empty with { WeightKg = 65.555m },
            empty with { HeightCm = 19 }, empty with { HeightCm = 261 }, empty with { HeightCm = 170.11m },
            empty with { SystolicBp = 39 }, empty with { SystolicBp = 301 }, empty with { SystolicBp = 120.111m },
            empty with { DiastolicBp = 19 }, empty with { DiastolicBp = 201 }, empty with { DiastolicBp = 80.111m },
            empty with { SystolicBp = 80, DiastolicBp = 80 },
            empty with { HeartRate = 19 }, empty with { HeartRate = 251 },
            empty with { TemperatureC = 29 }, empty with { TemperatureC = 46 }, empty with { TemperatureC = 36.55m },
            empty with { Spo2 = 39 }, empty with { Spo2 = 101 }, empty with { Spo2 = 98.111m },
            empty with { RespiratoryRate = 3 }, empty with { RespiratoryRate = 81 }, empty with { NurseNote = new('x', 1001) } })
            yield return [value];
    }

    [Theory, MemberData(nameof(InvalidMeasurements))]
    public async Task Invalid_measurements_never_reach_the_store(RecordVitalsRequest request)
    {
        var store = new Store();
        var error = await Assert.ThrowsAsync<VitalsException>(() => Service(store).RecordAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request, "key", Ct));
        Assert.Equal(VitalsError.InvalidInput, error.Error);
        Assert.Equal(0, store.Locks);
        Assert.Empty(store.Rows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has\nnewline")]
    [InlineData("é")]
    public async Task Invalid_keys_are_rejected(string key) => Assert.Equal(VitalsError.InvalidInput,
        (await Assert.ThrowsAsync<VitalsException>(() => Service(new Store()).RecordAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(), key, Ct))).Error);

    [Fact]
    public async Task Missing_or_empty_actor_and_oversized_key_are_rejected()
    {
        var id = Guid.NewGuid();
        foreach (var (encounter, staff, user, key) in new[] { (Guid.Empty, id, id, "k"), (id, Guid.Empty, id, "k"),
            (id, id, Guid.Empty, "k"), (id, id, id, new string('k', 129)) })
            Assert.Equal(VitalsError.InvalidInput, (await Assert.ThrowsAsync<VitalsException>(() =>
                Service(new Store()).RecordAsync(encounter, staff, user, new(), key, Ct))).Error);
    }

    [Theory]
    [InlineData(EncounterStatus.Completed)]
    [InlineData(EncounterStatus.Abandoned)]
    public async Task Ended_encounters_reject_new_measurements(EncounterStatus status)
    {
        var store = new Store { Encounter = new() { Status = status } };
        Assert.Equal(VitalsError.EncounterClosed, (await Assert.ThrowsAsync<VitalsException>(() =>
            Service(store).RecordAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(), "k", Ct))).Error);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task Missing_encounter_and_in_progress_idempotency_have_distinct_errors()
    {
        Assert.Equal(VitalsError.EncounterNotFound, (await Assert.ThrowsAsync<VitalsException>(() =>
            Service(new Store { Encounter = null }).RecordAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(), "k", Ct))).Error);
        Assert.Equal(VitalsError.RequestInProgress, (await Assert.ThrowsAsync<VitalsException>(() =>
            Service(new Store(), new Idempotency { Result = new IdempotencyBeginResult.InProgress(null) })
                .RecordAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(), "k", Ct))).Error);
    }

    [Theory]
    [InlineData(EncounterStatus.InProgress)]
    [InlineData(EncounterStatus.WaitingCLS)]
    [InlineData(EncounterStatus.ReturningDoctor)]
    public async Task Open_encounters_accept_partial_or_empty_measurements_without_inventing_bmi(EncounterStatus status)
    {
        var store = new Store { Encounter = new() { Status = status } };
        var service = Service(store);
        foreach (var request in new[] { new RecordVitalsRequest(), new(WeightKg: 65), new(SystolicBp: 120),
            new(DiastolicBp: 80), new(RespiratoryRate: 4, NurseNote: new('x', 1000)), new(RespiratoryRate: 80) })
        {
            var saved = await service.RecordAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request, "k", Ct);
            Assert.Null(saved.Bmi);
            Assert.Equal(Clock.Now, saved.VitalSign.RecordedAtUtc);
        }
        Assert.Equal(6, store.Rows.Count);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static RecordVitalsService Service(Store store, Idempotency? idempotency = null) => new(store, new Uow(), idempotency ?? new(), new Clock());
    private sealed class Store : IVitalsStore
    {
        public Encounter? Encounter { get; init; } = new() { Status = EncounterStatus.InProgress };
        public int Locks { get; private set; }
        public List<VitalSign> Rows { get; } = [];
        public Task<Encounter?> LockEncounterAsync(Guid id, CancellationToken ct) { Locks++; return Task.FromResult(Encounter); }
        public Task AppendAsync(VitalSign row, Guid actor, CancellationToken ct) { Rows.Add(row); return Task.CompletedTask; }
    }
    private sealed class Clock : IDbClock
    {
        public static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now);
    }
    private sealed class Uow : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
    private sealed class Idempotency : IIdempotencyStore
    {
        public IdempotencyBeginResult Result { get; init; } = new IdempotencyBeginResult.Started();
        public Task<IdempotencyBeginResult> BeginAsync(string a, string o, string k, string h, TimeSpan ttl, Guid? operation, CancellationToken ct) => Task.FromResult(Result);
        public Task CompleteAsync(string a, string o, string k, int status, string? body, Guid? operation, CancellationToken ct) => Task.CompletedTask;
    }
}
