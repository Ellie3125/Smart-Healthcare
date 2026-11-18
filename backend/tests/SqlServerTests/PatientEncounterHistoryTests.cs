using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Infrastructure;
using Billing.Application;
using Billing.Infrastructure.Persistence;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using DocumentsReporting.Api;
using DocumentsReporting.Application;
using DocumentsReporting.Infrastructure;
using DocumentsReporting.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Application;
using Scheduling.Api;
using Scheduling.Infrastructure;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

public sealed class PatientEncounterHistoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Route = "/api/documents/patient/encounters";

    [Fact]
    public async Task Patient_routes_reject_wrong_channel_and_published_record_without_diagnoses_returns_empty()
    {
        await using var f = await Host.CreateAsync();
        using (var wrong = f.Client(channel: Channels.Staff))
            foreach (var route in new[] { Route + "?patientProfileId=" + f.Patient, Route + "/" + f.Encounter })
            {
                using var denied = await wrong.GetAsync(route, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        await f.Db.QueryAsync($"UPDATE clinical.EncounterClinicalRecord SET Status='Published' WHERE EncounterId='{f.Encounter}'");
        using var client = f.Client();
        var detail = await client.GetFromJsonAsync<JsonElement>(Route + "/" + f.Encounter, Ct);
        Assert.Empty(detail.GetProperty("diagnoses").EnumerateArray());
        var list = await client.GetFromJsonAsync<JsonElement>(Route + "?patientProfileId=" + f.Patient, Ct);
        Assert.Equal(JsonValueKind.Null, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("primaryDiagnosis").ValueKind);
    }

    [Theory]
    [InlineData(true, "Final", "Published", "Main")]
    [InlineData(true, "Final", "Published", "Main", true)]
    [InlineData(false, "Final", "Draft", "Final")]
    [InlineData(false, "Initial", null, "First")]
    public async Task Published_history_uses_diagnoses_and_result_status_independent_of_files(
        bool primary, string type, string? resultStatus, string expected, bool postCls = false)
    {
        await using var f = await Host.CreateAsync();
        await f.Db.QueryAsync($"UPDATE clinical.EncounterClinicalRecord SET Status='Published' WHERE EncounterId='{f.Encounter}'");
        await f.DiagnosisAsync("First", "Initial", false, 0);
        await f.DiagnosisAsync(type, type, false, 1);
        if (primary) await f.DiagnosisAsync("Main", "Initial", true, 2);
        if (resultStatus is not null)
        {
            var order = await f.Db.InsertAsync("clinical.ServiceOrder", new { EncounterId = f.Encounter, OrderBatchStatus = "Confirmed" });
            var item = await f.Db.InsertAsync("clinical.ServiceOrderItem", new { ServiceOrderId = order, ItemStatus = "ServiceFinished", IsMandatory = true });
            await f.Db.InsertAsync("clinical.CLSResult", new { ServiceOrderItemId = item, ResultStatus = resultStatus });
        }
        if (postCls)
        {
            await f.Db.QueryAsync($"UPDATE clinical.EncounterClinicalRecord SET PostCLSConclusion='Published conclusion' WHERE EncounterId='{f.Encounter}'");
            var draftOrder = await f.Db.InsertAsync("clinical.ServiceOrder", new { EncounterId = f.Encounter, OrderBatchStatus = "Confirmed" });
            await f.Db.InsertAsync("clinical.ServiceOrderItem", new { ServiceOrderId = draftOrder, ItemStatus = "Ordered", IsMandatory = true });
        }
        using var client = f.Client();
        var list = await client.GetFromJsonAsync<JsonElement>(Route + "?patientProfileId=" + f.Patient, Ct);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(expected, row.GetProperty("primaryDiagnosis").GetString());
        Assert.Equal(resultStatus == "Published", row.GetProperty("hasClsResults").GetBoolean());
        Assert.False(row.GetProperty("hasPublishedAttachments").GetBoolean());
        var detail = await client.GetFromJsonAsync<JsonElement>(Route + "/" + f.Encounter, Ct);
        var diagnoses = detail.GetProperty("diagnoses").EnumerateArray().ToArray();
        Assert.Equal(primary ? 3 : 2, diagnoses.Length);
        Assert.Contains(diagnoses, d => d.GetProperty("diagnosisName").GetString() == "First"
            && d.GetProperty("icd10Code").GetString() == "K29.7" && d.GetProperty("diagnosisType").GetString() == "Initial");
        Assert.Equal(primary, diagnoses.Any(d => d.GetProperty("isPrimary").GetBoolean()));
        Assert.Equal(postCls ? 1 : 0, detail.GetProperty("clsResults").GetArrayLength());
        Assert.All(detail.GetProperty("clsResults").EnumerateArray(), r => Assert.Equal("Published", r.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Draft_detail_is_404_and_list_never_exposes_draft_diagnoses_even_if_encounter_completed()
    {
        await using var f = await Host.CreateAsync();
        await f.DiagnosisAsync("Secret diagnosis", "Final", true, 0);
        await f.Db.QueryAsync($"UPDATE clinical.Encounter SET Status='Completed',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{f.Encounter}'");
        using var client = f.Client();
        using var response = await client.GetAsync(Route + "/" + f.Encounter, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(Route + "?patientProfileId=" + f.Patient, Ct);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("primaryDiagnosis").ValueKind);
        Assert.False(row.GetProperty("isPublished").GetBoolean());
        Assert.DoesNotContain("Secret diagnosis", list.GetRawText());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    [InlineData("Revoked")]
    [InlineData("OtherAccount")]
    public async Task Unverified_or_foreign_profile_is_blocked_on_both_routes(string status)
    {
        await using var f = await Host.CreateAsync();
        if (status != "OtherAccount")
            await f.Db.QueryAsync($"UPDATE [identity].PatientRepresentative SET VerificationStatus='{status}' WHERE PatientId='{f.Patient}'");
        using var client = f.Client(actor: status == "OtherAccount" ? Guid.NewGuid() : null);
        foreach (var route in new[] { Route + "?patientProfileId=" + f.Patient, Route + "/" + f.Encounter })
        {
            using var response = await client.GetAsync(route, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private sealed class Host(WebApplication app, TestDatabase db, Guid actor, Guid patient, Guid encounter) : IAsyncDisposable
    {
        private readonly Guid _actor = actor;
        public TestDatabase Db => db;
        public Guid Patient => patient;
        public Guid Encounter => encounter;
        public static async Task<Host> CreateAsync()
        {
            var db = await TestDatabase.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "History.Tests";
            var cs = TestDatabase.RequiredConnectionString();
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs).AddClinicalInfrastructure(cs)
                .AddSchedulingInfrastructure(cs).AddSchedulingApplication().AddSchedulingApi().AddBillingInfrastructure(cs).AddBillingApplication()
                .AddDocumentsReportingInfrastructure(cs).AddDocumentsReportingApplication();
            builder.Services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<ClinicalDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<BillingDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<DocumentsDbContext>(o => new(o)));
            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
                _ = scope.ServiceProvider.GetRequiredService<DocumentsReporting.Contracts.IDocumentsModule>();
            app.MapDocumentsReportingEndpoints(); await app.StartAsync(Ct);
            var actor = await db.InsertAsync("identity.AppUser", new { IsActive = true });
            var patient = await db.InsertAsync("identity.Patient", new { Gender = "Male" });
            await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = actor, PatientId = patient, Relationship = "Self", VerificationStatus = "Verified", IsActive = true });
            var encounter = await db.InsertAsync("clinical.Encounter", new { PatientId = patient, Status = "InProgress" });
            await db.InsertAsync("clinical.EncounterClinicalRecord", new { EncounterId = encounter, Status = "Draft" });
            return new(app, db, actor, patient, encounter);
        }
        public Task<Guid> DiagnosisAsync(string name, string type, bool primary, int offset) => db.InsertAsync("clinical.EncounterDiagnosis",
            new { EncounterId = encounter, DiagnosisName = name, DiagnosisType = type, ICD10Code = "K29.7", IsPrimary = primary, DiagnosedAtUtc = new DateTime(2026, 11, 18, 1, offset, 0) });
        public HttpClient Client(Guid? actor = null, string channel = Channels.Patient)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            var token = new JwtSecurityToken("History.Tests", "History.Tests", [new("sub", (actor ?? _actor).ToString()), new("role", Roles.Patient), new(AppClaims.Channel, channel)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(Ct); await app.DisposeAsync(); await db.DisposeAsync(); }
    }
}
