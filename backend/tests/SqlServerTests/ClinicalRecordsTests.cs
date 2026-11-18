using System.Net;
using System.Net.Http.Json;
using Clinical.Application.Records;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalRecordsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Legacy_published_record_without_baseline_revision_is_rejected_without_losing_history()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        await using (var scope = f.App.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<EncounterClinicalRecord>().Where(x => x.Id == f.RecordId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EncounterClinicalRecordStatus.Published).SetProperty(x => x.PostCLSConclusion, "Legacy conclusion"), Ct);
        var record = await f.RecordAsync();
        using var client = f.Client();
        using var response = await f.SendAsync(client, "amend", new { version = record.RowVersion, reason = "Reason", diagnoses = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("HistoryUnavailable", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Legacy conclusion", (await f.RecordAsync()).PostCLSConclusion);
    }

    [Theory]
    [InlineData(RecordAction.Draft)]
    [InlineData(RecordAction.Publish)]
    public async Task Failure_after_sql_writes_rolls_back_record_diagnoses_revision_audit_and_idempotency(RecordAction action)
    {
        await using var f = await ClinicalRecordFixture.CreateAsync(services => services.AddScoped<IClinicalRecordStore>(sp =>
            new FailingStore(ActivatorUtilities.CreateInstance<EfClinicalRecordStore>(sp))));
        var record = await f.RecordAsync();
        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            var request = new ClinicalRecordRequest(record.RowVersion, [new("Initial", "A00", "Diagnosis")], Symptoms: "Must rollback");
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ClinicalRecordService>()
                .WriteAsync(f.EncounterId, f.DoctorId, f.UserId, action, request, "rollback", Ct));
        }
        var unchanged = await f.RecordAsync();
        Assert.Equal(record.RowVersion, unchanged.RowVersion);
        Assert.Null(unchanged.Symptoms);
        Assert.Equal(EncounterClinicalRecordStatus.Draft, unchanged.Status);
        Assert.Equal(0, await f.RevisionCountAsync());
        await using var check = f.App.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<EncounterDiagnosis>().AnyAsync(x => x.EncounterId == f.EncounterId, Ct));
        var shared = check.ServiceProvider.GetRequiredService<SharedDbContext>();
        Assert.False(await shared.Set<AuditLog>().AnyAsync(x => x.RecordId == f.RecordId, Ct));
        Assert.False(await shared.Set<ApiIdempotencyRecord>().AnyAsync(x => x.CallerScope == "AppUser:" + f.UserId.ToString("D"), Ct));
    }

    [Fact]
    public async Task Store_rejects_missing_transaction_and_missing_resources_have_named_errors()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        await using (var scope = f.App.Services.CreateAsyncScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IClinicalRecordStore>().LockEncounterAsync(f.EncounterId, Ct));
        using var client = f.Client();
        var record = await f.RecordAsync();
        using var missing = new HttpRequestMessage(HttpMethod.Post, $"/api/clinical/staff/doctor/encounters/{Guid.NewGuid()}/publish")
            { Content = JsonContent.Create(new { version = record.RowVersion }) };
        missing.Headers.Add("Idempotency-Key", "missing");
        using var response = await client.SendAsync(missing, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("EncounterNotFound", await response.Content.ReadAsStringAsync(Ct));
        await using (var scope = f.App.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<EncounterClinicalRecord>().Where(x => x.Id == f.RecordId).ExecuteDeleteAsync(Ct);
        using var noRecord = await f.SendAsync(client, "publish", new { version = record.RowVersion });
        Assert.Equal(HttpStatusCode.NotFound, noRecord.StatusCode);
        Assert.Contains("ClinicalRecordNotFound", await noRecord.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task In_progress_key_returns_conflict_without_modifying_record()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync(services => services.AddScoped<IIdempotencyStore, InProgress>());
        using var client = f.Client();
        var record = await f.RecordAsync();
        using var response = await f.SendAsync(client, "draft", new { version = record.RowVersion, diagnoses = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("RequestInProgress", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(record.RowVersion, (await f.RecordAsync()).RowVersion);
    }

    private sealed class FailingStore(IClinicalRecordStore inner) : IClinicalRecordStore
    {
        public Task<Encounter?> LockEncounterAsync(Guid id, CancellationToken ct) => inner.LockEncounterAsync(id, ct);
        public Task<EncounterClinicalRecord?> GetRecordAsync(Guid id, CancellationToken ct) => inner.GetRecordAsync(id, ct);
        public Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesAsync(Guid id, CancellationToken ct) => inner.GetDiagnosesAsync(id, ct);
        public async Task SaveAsync(EncounterClinicalRecord record, IReadOnlyList<EncounterDiagnosis> diagnoses, RecordAction action, string? reason, Guid actor, CancellationToken ct)
        {
            await inner.SaveAsync(record, diagnoses, action, reason, actor, ct);
            throw new InvalidOperationException("Injected after SQL writes.");
        }
    }
    private sealed class InProgress : IIdempotencyStore
    {
        public Task<IdempotencyBeginResult> BeginAsync(string a, string o, string k, string h, TimeSpan ttl, Guid? operation, CancellationToken ct) =>
            Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(null));
        public Task CompleteAsync(string a, string o, string k, int status, string? body, Guid? operation, CancellationToken ct) =>
            throw new InvalidOperationException("An in-progress request must not complete.");
    }
}
