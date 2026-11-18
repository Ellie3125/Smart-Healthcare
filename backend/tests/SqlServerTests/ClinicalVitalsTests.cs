using Clinical.Application.Vitals;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalVitalsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Encounter_lock_is_held_until_measurements_commit_and_prevents_concurrent_completion()
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = await ClinicalVitalsFixture.CreateAsync(services => services.AddScoped<IVitalsStore>(sp =>
            new PausedStore(ActivatorUtilities.CreateInstance<EfVitalsStore>(sp), locked, release)));
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using (var timeout = new SqlCommand("SET LOCK_TIMEOUT 0", connection)) await timeout.ExecuteNonQueryAsync(Ct);
        await using var complete = new SqlCommand("UPDATE clinical.Encounter SET Status=N'Completed' WHERE Id=@id", connection);
        complete.Parameters.AddWithValue("@id", fixture.EncounterId);
        var recording = fixture.RecordAsync("locked");
        await Task.WhenAny(locked.Task, recording).WaitAsync(Ct);
        if (!locked.Task.IsCompleted) { await recording; Assert.Fail("Recording returned before taking the encounter lock."); }
        try
        {
            Assert.Equal(1222, (await Assert.ThrowsAsync<SqlException>(() => complete.ExecuteNonQueryAsync(Ct))).Number);
        }
        finally { release.TrySetResult(); }
        await recording;
        Assert.Equal(1, await complete.ExecuteNonQueryAsync(Ct));
        Assert.Equal(VitalsError.EncounterClosed, (await Assert.ThrowsAsync<VitalsException>(() => fixture.RecordAsync("after-close"))).Error);
        Assert.Equal((1, 1, 1), await fixture.CountsAsync());
    }

    [Fact]
    public async Task Store_refuses_a_write_path_without_the_clinical_transaction_participant()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IVitalsStore>()
            .LockEncounterAsync(fixture.EncounterId, Ct));
        Assert.Equal((0, 0, 0), await fixture.CountsAsync());
    }

    [Fact]
    public async Task Parallel_same_key_records_once_and_replays_even_after_encounter_closes()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        var results = await Task.WhenAll(fixture.RecordAsync("same"), fixture.RecordAsync("same"));
        Assert.Equal(results[0], results[1]);
        var saved = results[0];
        Assert.Equal(saved, await fixture.RecordAsync("same", ClinicalVitalsFixture.Request with
            { WeightKg = 70.00m, HeightCm = 175.0m, SystolicBp = 120.00m, DiastolicBp = 80.00m, TemperatureC = 36.50m, Spo2 = 98.00m }));
        Assert.Equal(22.9m, saved.Bmi);
        Assert.Equal(fixture.StaffId, saved.VitalSign.RecordedByStaffId);
        Assert.Equal(16, saved.VitalSign.RespiratoryRate);
        Assert.Equal(DateTimeKind.Utc, saved.VitalSign.RecordedAtUtc.Kind);
        Assert.Equal((1, 1, 1), await fixture.CountsAsync());
        Assert.Equal(VitalsError.IdempotencyConflict, (await Assert.ThrowsAsync<VitalsException>(() =>
            fixture.RecordAsync("same", ClinicalVitalsFixture.Request with { NurseNote = "different" }))).Error);
        await fixture.SetStatusAsync(EncounterStatus.Completed);
        Assert.Equal(saved, await fixture.RecordAsync("same"));
        Assert.Equal(VitalsError.EncounterClosed, (await Assert.ThrowsAsync<VitalsException>(() => fixture.RecordAsync("new"))).Error);
        Assert.Equal((1, 1, 1), await fixture.CountsAsync());
    }

    [Fact]
    public async Task New_keys_append_measurements_and_audit_omits_measurements_and_notes()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        var first = await fixture.RecordAsync("one");
        var second = await fixture.RecordAsync("two", ClinicalVitalsFixture.Request with { NurseNote = "second private note", RespiratoryRate = 20 });
        Assert.NotEqual(first.VitalSign.Id, second.VitalSign.Id);
        Assert.Equal((2, 2, 2), await fixture.CountsAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        Assert.Equal(ClinicalVitalsFixture.Request.NurseNote, (await db.Set<VitalSign>().SingleAsync(x => x.Id == first.VitalSign.Id, Ct)).NurseNote);
        var audits = await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>()
            .Where(x => x.PerformedByUserId == fixture.UserId).ToListAsync(Ct);
        Assert.All(audits, x =>
        {
            Assert.Equal("clinical.VitalSign", x.TableName);
            Assert.Contains(fixture.StaffId.ToString(), x.NewValuesJson!);
            Assert.DoesNotContain("private", x.NewValuesJson!);
            Assert.DoesNotContain("WeightKg", x.NewValuesJson!);
        });
    }

    [Fact]
    public async Task Failure_after_vitals_and_audit_rolls_back_all_writes_including_idempotency()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync(services =>
            services.AddScoped<IVitalsStore>(sp => new FailingStore(ActivatorUtilities.CreateInstance<EfVitalsStore>(sp))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RecordAsync("rollback"));
        Assert.Equal((0, 0, 0), await fixture.CountsAsync());
    }

    [Theory]
    [InlineData(EncounterStatus.Completed)]
    [InlineData(EncounterStatus.Abandoned)]
    public async Task Closed_encounter_does_not_leave_a_vital_audit_or_idempotency_record(EncounterStatus status)
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        await fixture.SetStatusAsync(status);
        Assert.Equal(VitalsError.EncounterClosed, (await Assert.ThrowsAsync<VitalsException>(() => fixture.RecordAsync("closed"))).Error);
        Assert.Equal((0, 0, 0), await fixture.CountsAsync());
    }

    private sealed class FailingStore(IVitalsStore inner) : IVitalsStore
    {
        public Task<Encounter?> LockEncounterAsync(Guid id, CancellationToken ct) => inner.LockEncounterAsync(id, ct);
        public async Task AppendAsync(VitalSign row, Guid actor, CancellationToken ct)
        {
            await inner.AppendAsync(row, actor, ct);
            throw new InvalidOperationException("Injected failure after SQL writes.");
        }
    }
    private sealed class PausedStore(IVitalsStore inner, TaskCompletionSource locked, TaskCompletionSource release) : IVitalsStore
    {
        public async Task<Encounter?> LockEncounterAsync(Guid id, CancellationToken ct)
        {
            var encounter = await inner.LockEncounterAsync(id, ct);
            locked.TrySetResult();
            await release.Task.WaitAsync(ct);
            return encounter;
        }
        public Task AppendAsync(VitalSign row, Guid actor, CancellationToken ct) => inner.AppendAsync(row, actor, ct);
    }
}

internal sealed class ClinicalVitalsFixture : IAsyncDisposable
{
    public ServiceProvider Services { get; }
    public Guid EncounterId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid StaffId { get; } = Guid.NewGuid();
    public static RecordVitalsRequest Request => new(70, 175, 120, 80, 72, 36.5m, 98, 16, "first private note");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ClinicalVitalsFixture(ServiceProvider services) => Services = services;

    public static async Task<ClinicalVitalsFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var services = new ServiceCollection().AddSharedInfrastructure(cs).AddClinicalInfrastructure(cs)
            .AddUnitOfWorkParticipant<ClinicalDbContext>();
        configure?.Invoke(services);
        var fixture = new ClinicalVitalsFixture(services.BuildServiceProvider());
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        db.Add(new Encounter { Id = fixture.EncounterId, AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            OriginalDoctorId = Guid.NewGuid(), OriginalRoomId = Guid.NewGuid(), SpecialtySnapshot = "Test",
            DoctorRankSnapshot = "Test", Status = EncounterStatus.InProgress, CheckedInAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(Ct);
        return fixture;
    }

    public async Task<RecordedVitals> RecordAsync(string key, RecordVitalsRequest? request = null)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RecordVitalsService>().RecordAsync(
            EncounterId, StaffId, UserId, request ?? Request, key, Ct);
    }

    public async Task SetStatusAsync(EncounterStatus status)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().Where(x => x.Id == EncounterId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), Ct);
    }

    public async Task<(int Vitals, int Audits, int Idempotency)> CountsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        return (await db.Set<VitalSign>().CountAsync(x => x.EncounterId == EncounterId, Ct),
            await shared.Set<AuditLog>().CountAsync(x => x.PerformedByUserId == UserId, Ct),
            await shared.Set<ApiIdempotencyRecord>().CountAsync(x => x.CallerScope == "AppUser:" + UserId.ToString("D"), Ct));
    }

    public async ValueTask DisposeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        await shared.Set<AuditLog>().Where(x => x.PerformedByUserId == UserId).ExecuteDeleteAsync(Ct);
        await shared.Set<ApiIdempotencyRecord>().Where(x => x.CallerScope == "AppUser:" + UserId.ToString("D")).ExecuteDeleteAsync(Ct);
        await db.Set<VitalSign>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
        await db.Set<Encounter>().Where(x => x.Id == EncounterId).ExecuteDeleteAsync(Ct);
        await Services.DisposeAsync();
    }
}
