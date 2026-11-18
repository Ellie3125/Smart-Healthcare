using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Clinical.Api;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalRecordsEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verified_patient_cannot_read_a_draft_but_can_read_the_published_record()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        await using var scope = f.App.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityPatients.Infrastructure.Persistence.IdentityPatientsDbContext>();
        var patientId = (await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().SingleAsync(x => x.Id == f.EncounterId, Ct)).PatientId;
        identity.Add(new IdentityPatients.Domain.Entities.AppUser { Id = f.UserId, UserName = "record-test-" + f.UserId.ToString("N"), PasswordHash = "test-only", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        identity.Add(new IdentityPatients.Domain.Entities.Patient { Id = patientId, FullName = "Test patient", DateOfBirth = new(2000, 1, 1),
            PhoneNumber = "0900000000", Address = "Test", CreatedAtUtc = DateTime.UtcNow });
        identity.Add(new IdentityPatients.Domain.Entities.PatientRepresentative { Id = Guid.NewGuid(), AppUserId = f.UserId, PatientId = patientId,
            IsActive = true, VerificationStatus = IdentityPatients.Domain.Enums.VerificationStatus.Verified, CreatedAtUtc = DateTime.UtcNow });
        await identity.SaveChangesAsync(Ct);
        try
        {
            using var patient = f.Client(Roles.Patient, Channels.Patient);
            using var draft = await patient.GetAsync($"/api/clinical/patient/encounters/{f.EncounterId}/record", Ct);
            Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
            using var doctor = f.Client();
            using var publish = await f.SendAsync(doctor, "publish", new { version = (await f.RecordAsync()).RowVersion });
            Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
            using var published = await patient.GetAsync($"/api/clinical/patient/encounters/{f.EncounterId}/record", Ct);
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        }
        finally
        {
            await identity.Set<IdentityPatients.Domain.Entities.PatientRepresentative>().Where(x => x.AppUserId == f.UserId).ExecuteDeleteAsync(Ct);
            await identity.Set<IdentityPatients.Domain.Entities.Patient>().Where(x => x.Id == patientId).ExecuteDeleteAsync(Ct);
            await identity.Set<IdentityPatients.Domain.Entities.AppUser>().Where(x => x.Id == f.UserId).ExecuteDeleteAsync(Ct);
        }
    }

    [Fact]
    public async Task Draft_publish_amend_keep_immutable_clinical_and_diagnosis_snapshots()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var client = f.Client();
        var initial = await f.RecordAsync();
        using var draft = await f.SendAsync(client, "draft", new { version = initial.RowVersion,
            symptoms = "private symptoms", postCLSConclusion = "first conclusion",
            diagnoses = new[] { new { diagnosisType = "Initial", icd10Code = " a00.1 ", diagnosisName = " Test diagnosis ", isPrimary = true } } });
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var saved = await draft.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("A00.1", saved.GetProperty("diagnoses")[0].GetProperty("icD10Code").GetString());
        Assert.Equal(0, await f.RevisionCountAsync());
        using var publish = await f.SendAsync(client, "publish", new { version = saved.GetProperty("version").GetString() });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Published", published.GetProperty("record").GetProperty("status").GetString());
        using var repeated = await f.SendAsync(client, "publish", new { version = saved.GetProperty("version").GetString() });
        await Error(repeated, HttpStatusCode.Conflict, "AlreadyPublished");
        using var forbiddenDraft = await f.SendAsync(client, "draft", new { version = published.GetProperty("version").GetString(), diagnoses = Array.Empty<object>() });
        await Error(forbiddenDraft, HttpStatusCode.Conflict, "AlreadyPublished");
        var amendment = new { version = published.GetProperty("version").GetString(), postCLSConclusion = "corrected conclusion",
            reason = "Correct a transcription", diagnoses = new[] { new { diagnosisType = "Final", icd10Code = " b01 ", diagnosisName = "Corrected diagnosis" } } };
        using var amend = await f.SendAsync(client, "amend", amendment, "amend");
        Assert.Equal(HttpStatusCode.OK, amend.StatusCode);
        var amended = await amend.Content.ReadAsStringAsync(Ct);
        using var replay = await f.SendAsync(client, "amend", amendment, "amend");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(amended, await replay.Content.ReadAsStringAsync(Ct));
        await using var scope = f.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var revisions = await db.Set<EncounterClinicalRecordRevision>().Where(x => x.ClinicalRecordId == f.RecordId)
            .OrderBy(x => x.RevisionNumber).ToListAsync(Ct);
        Assert.Equal(2, revisions.Count);
        Assert.Equal(ClinicalRecordRevisionKind.Published, revisions[0].Kind);
        Assert.Equal("first conclusion", revisions[0].PostCLSConclusion);
        Assert.Contains("A00.1", revisions[0].DiagnosesJson);
        Assert.Equal(ClinicalRecordRevisionKind.Amended, revisions[1].Kind);
        Assert.Equal(2, revisions[1].RevisionNumber);
        Assert.Equal("corrected conclusion", revisions[1].PostCLSConclusion);
        Assert.Contains("B01", revisions[1].DiagnosesJson);
        Assert.Equal("Correct a transcription", revisions[1].Reason);
        Assert.All(revisions, x => Assert.Equal(f.DoctorId, x.ChangedByDoctorId));
        var audit = await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>()
            .Where(x => x.RecordId == f.RecordId).ToListAsync(Ct);
        Assert.Equal(3, audit.Count);
        Assert.All(audit, x => { Assert.Equal(f.UserId, x.PerformedByUserId); Assert.DoesNotContain("conclusion", x.NewValuesJson!); });
    }

    [Fact]
    public async Task Writes_require_staff_doctor_and_original_doctor_claim_never_body_identity()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        foreach (var action in new[] { "draft", "publish", "amend" })
        {
            using var anonymous = f.Client(authenticate: false);
            using var unauthenticated = await f.SendAsync(anonymous, action, new { });
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            foreach (var (role, channel) in new[] { (Roles.Doctor, Channels.Patient), (Roles.Patient, Channels.Patient),
                (Roles.Nurse, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
            {
                using var wrong = f.Client(role, channel);
                using var denied = await f.SendAsync(wrong, action, new { });
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
            using var otherDoctor = f.Client(doctorId: Guid.NewGuid());
            using var other = await f.SendAsync(otherDoctor, action, new { version = (await f.RecordAsync()).RowVersion,
                doctorId = f.DoctorId, reason = "Reason", diagnoses = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        }
        Assert.Equal(0, await f.RevisionCountAsync());
    }

    [Fact]
    public async Task Invalid_codes_names_versions_and_request_shapes_never_write()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var client = f.Client();
        var version = (await f.RecordAsync()).RowVersion;
        using var read = await client.GetAsync(f.Route("record"), Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(Convert.ToBase64String(version), (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("version").GetString());
        foreach (var (code, name, type) in new[] { ("A1", "Name", "Initial"), ("AA00", "Name", "Initial"),
            ("A00.12345", "Name", "Initial"), ("A00\n", "Name", "Other"), ("A00", " ", "Initial"),
            ("A00.", "Name", "Final"), ("A00.1\nB00", "Name", "Initial"), ("A00", new string('x', 201), "Initial") })
        {
            using var response = await f.SendAsync(client, "draft", new { version,
                diagnoses = new[] { new { icd10Code = code, diagnosisName = name, diagnosisType = type } } });
            await Error(response, HttpStatusCode.BadRequest, "InvalidInput");
        }
        foreach (var json in new[] { "null", "{", "{}", "{\"version\":\"not-base64\"}", "{\"version\":null}",
            "{\"version\":\"AQ==\",\"diagnoses\":[]}", "{\"version\":\"AQIDBAUGBwg=\",\"diagnoses\":[null]}" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, f.Route("draft")) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await client.SendAsync(request, Ct);
            await Error(response, HttpStatusCode.BadRequest, "InvalidInput");
        }
        using var unsupported = await client.PutAsync(f.Route("draft"), new StringContent("plain"), Ct);
        await Error(unsupported, HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType");
        using var noKey = await client.PutAsJsonAsync(f.Route("draft"), new { version, diagnoses = Array.Empty<object>() }, Ct);
        await Error(noKey, HttpStatusCode.BadRequest, "InvalidInput");
        using var notPublished = await f.SendAsync(client, "amend", new { version, reason = "Reason", diagnoses = Array.Empty<object>() });
        await Error(notPublished, HttpStatusCode.Conflict, "NotPublished");
        foreach (var reason in new[] { "", " ", new string('x', 1001) })
        {
            using var invalidReason = await f.SendAsync(client, "amend", new { version, reason, diagnoses = Array.Empty<object>() });
            await Error(invalidReason, HttpStatusCode.BadRequest, "InvalidInput");
        }
        Assert.Equal(version, (await f.RecordAsync()).RowVersion);
        Assert.Equal(0, await f.RevisionCountAsync());
    }

    [Fact]
    public async Task Concurrent_drafts_serialize_and_stale_version_or_key_conflict_cannot_overwrite()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var firstClient = f.Client();
        using var secondClient = f.Client();
        var body = new { version = (await f.RecordAsync()).RowVersion, symptoms = "First", diagnoses = Array.Empty<object>() };
        var responses = await Task.WhenAll(f.SendAsync(firstClient, "draft", body), f.SendAsync(secondClient, "draft", body));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            await Error(Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "VersionConflict");
        }
        finally { foreach (var response in responses) response.Dispose(); }
        var newBody = new { version = (await f.RecordAsync()).RowVersion, symptoms = "Second", diagnoses = Array.Empty<object>() };
        using var saved = await f.SendAsync(firstClient, "draft", newBody, "replay");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var replay = await f.SendAsync(firstClient, "draft", newBody, "replay");
        Assert.Equal(await saved.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var conflict = await f.SendAsync(firstClient, "draft", new { newBody.version, symptoms = "Different", newBody.diagnoses }, "replay");
        await Error(conflict, HttpStatusCode.Conflict, "IdempotencyConflict");
    }

    [Theory]
    [InlineData(EncounterStatus.Completed)]
    [InlineData(EncounterStatus.Abandoned)]
    public async Task Closed_encounters_reject_every_write(EncounterStatus status)
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        await using var scope = f.App.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().Where(x => x.Id == f.EncounterId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), Ct);
        using var client = f.Client();
        foreach (var action in new[] { "draft", "publish", "amend" })
        {
            using var response = await f.SendAsync(client, action, new { version = (await f.RecordAsync()).RowVersion, reason = "Reason", diagnoses = Array.Empty<object>() });
            await Error(response, HttpStatusCode.Conflict, "EncounterClosed");
        }
    }

    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains($"\"code\":\"{code}\"", await response.Content.ReadAsStringAsync(Ct));
    }
}

internal sealed class ClinicalRecordFixture(WebApplication app) : IAsyncDisposable
{
    public WebApplication App { get; } = app;
    public Guid EncounterId { get; } = Guid.NewGuid();
    public Guid RecordId { get; } = Guid.NewGuid();
    public Guid DoctorId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ClinicalRecordFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Records.Tests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
            .AddClinicalInfrastructure(cs).AddUnitOfWorkParticipant<ClinicalDbContext>().AddClinicalApi();
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.MapClinicalEndpoints();
        await app.StartAsync(Ct);
        var f = new ClinicalRecordFixture(app);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var now = DateTime.UtcNow;
        db.Add(new Encounter { Id = f.EncounterId, AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(), OriginalDoctorId = f.DoctorId,
            OriginalRoomId = Guid.NewGuid(), SpecialtySnapshot = "Test", DoctorRankSnapshot = "Test", Status = EncounterStatus.InProgress, CheckedInAtUtc = now });
        db.Add(new EncounterClinicalRecord { Id = f.RecordId, EncounterId = f.EncounterId, RecordedAtUtc = now, LastUpdatedUtc = now,
            UpdatedByDoctorId = f.DoctorId, Status = EncounterClinicalRecordStatus.Draft });
        await db.SaveChangesAsync(Ct);
        return f;
    }

    public HttpClient Client(string role = Roles.Doctor, string channel = Channels.Staff, Guid? doctorId = null, bool authenticate = true)
    {
        var client = new HttpClient { BaseAddress = new(App.Urls.Single()) };
        if (authenticate)
        {
            var token = new JwtSecurityToken("Records.Tests", "Records.Tests",
                [new("sub", UserId.ToString()), new("role", role), new(AppClaims.Channel, channel), new(AppClaims.DoctorId, (doctorId ?? DoctorId).ToString())],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(App.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    public string Route(string action) => $"/api/clinical/staff/doctor/encounters/{EncounterId}/{action}";
    public Task<HttpResponseMessage> SendAsync(HttpClient client, string action, object body, string? key = null)
    {
        var request = new HttpRequestMessage(action == "draft" ? HttpMethod.Put : HttpMethod.Post, Route(action)) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return SendAndDisposeAsync(client, request);
    }
    private static async Task<HttpResponseMessage> SendAndDisposeAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request) return await client.SendAsync(request, Ct);
    }
    public async Task<EncounterClinicalRecord> RecordAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<EncounterClinicalRecord>().AsNoTracking().SingleAsync(x => x.Id == RecordId, Ct);
    }
    public async Task<int> RevisionCountAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<EncounterClinicalRecordRevision>().CountAsync(x => x.ClinicalRecordId == RecordId, Ct);
    }
    public async ValueTask DisposeAsync()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            await shared.Set<AuditLog>().Where(x => x.RecordId == RecordId).ExecuteDeleteAsync(Ct);
            await shared.Set<ApiIdempotencyRecord>().Where(x => x.CallerScope == "AppUser:" + UserId.ToString("D")).ExecuteDeleteAsync(Ct);
            await db.Set<EncounterClinicalRecordRevision>().Where(x => x.ClinicalRecordId == RecordId).ExecuteDeleteAsync(Ct);
            await db.Set<EncounterDiagnosis>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
            await db.Set<EncounterClinicalRecord>().Where(x => x.Id == RecordId).ExecuteDeleteAsync(Ct);
            await db.Set<Encounter>().Where(x => x.Id == EncounterId).ExecuteDeleteAsync(Ct);
        }
        await App.StopAsync(Ct);
        await App.DisposeAsync();
    }
}
