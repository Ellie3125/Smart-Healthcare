using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Clinical.Api;
using Clinical.Application.Vitals;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalVitalsEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vitals_write_requires_nurse_staff_and_records_the_verified_staff_instead_of_body_actor()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        var directory = new Directory(fixture.StaffId);
        await using var app = await App(directory);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var route = Route(fixture.EncounterId);
        using var anonymous = await client.PostAsync(route, null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Nurse, Channels.Patient),
            (Roles.Doctor, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            Authorize(client, app.Configuration, fixture.UserId, role, channel);
            using var forbidden = await client.PostAsync(route, null, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        Assert.Null(directory.LastUserId);
        Authorize(client, app.Configuration, fixture.UserId);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "record");
        using var result = await client.PostAsJsonAsync(route, new { weightKg = 70, heightCm = 175, systolicBp = 120,
            diastolicBp = 80, heartRate = 72, temperatureC = 36.5, spo2 = 98, respiratoryRate = 16, nurseNote = "private note",
            recordedByStaffId = Guid.NewGuid(), userId = Guid.NewGuid(), encounterId = Guid.NewGuid() }, Ct);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var saved = (await result.Content.ReadFromJsonAsync<RecordedVitals>(Ct))!;
        Assert.Equal(fixture.EncounterId, saved.VitalSign.EncounterId);
        Assert.Equal(fixture.StaffId, saved.VitalSign.RecordedByStaffId);
        Assert.Equal(fixture.UserId, directory.LastUserId);
        Assert.Equal((1, 1, 1), await fixture.CountsAsync());
    }

    [Fact]
    public async Task Invalid_body_headers_actor_and_directory_fail_closed()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        var directory = new Directory(fixture.StaffId);
        await using var app = await App(directory);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var route = Route(fixture.EncounterId);
        Authorize(client, app.Configuration, Guid.Empty);
        using var noActor = await client.PostAsJsonAsync(route, ClinicalVitalsFixture.Request, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, noActor.StatusCode);
        Authorize(client, app.Configuration, fixture.UserId);
        foreach (var missing in new Guid?[] { null, Guid.Empty })
        {
            directory.StaffId = missing;
            using var forbidden = await client.PostAsJsonAsync(route, ClinicalVitalsFixture.Request, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        directory.StaffId = fixture.StaffId;
        using var unsupported = await client.PostAsync(route, new StringContent("plain"), Ct);
        await Error(unsupported, HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType");
        using var missingKey = await client.PostAsJsonAsync(route, ClinicalVitalsFixture.Request, Ct);
        await Error(missingKey, HttpStatusCode.BadRequest, "InvalidInput");
        client.DefaultRequestHeaders.Add("Idempotency-Key", new[] { "one", "two" });
        using var duplicateKey = await client.PostAsJsonAsync(route, ClinicalVitalsFixture.Request, Ct);
        await Error(duplicateKey, HttpStatusCode.BadRequest, "InvalidInput");
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "errors");
        foreach (var json in new[] { "{", "null", "{\"heartRate\":\"invalid\"}", "{\"respiratoryRate\":81}",
            "{\"heightCm\":170.01}", "{\"systolicBp\":80,\"diastolicBp\":120}" })
        {
            using var invalid = await client.PostAsync(route, new StringContent(json, Encoding.UTF8, "application/json"), Ct);
            await Error(invalid, HttpStatusCode.BadRequest, "InvalidInput");
        }
        using var missingEncounter = await client.PostAsJsonAsync(Route(Guid.NewGuid()), ClinicalVitalsFixture.Request, Ct);
        await Error(missingEncounter, HttpStatusCode.NotFound, "EncounterNotFound");
        await fixture.SetStatusAsync(Clinical.Domain.Enums.EncounterStatus.Completed);
        using var closed = await client.PostAsJsonAsync(route, ClinicalVitalsFixture.Request, Ct);
        await Error(closed, HttpStatusCode.Conflict, "EncounterClosed");
        Assert.Equal((0, 0, 0), await fixture.CountsAsync());
    }

    [Fact]
    public async Task Production_module_maps_the_route_and_verifies_active_nurse_through_real_identity_contract()
    {
        await using var fixture = await ClinicalVitalsFixture.CreateAsync();
        await using var app = await App(null, prototype: false);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        try
        {
            await using (var seed = app.Services.CreateAsyncScope())
            {
                var db = seed.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
                db.Add(new IdentityPatients.Domain.Entities.AppUser { Id = fixture.UserId,
                    UserName = "vitals-" + fixture.UserId.ToString("N"), PasswordHash = "unused-test-hash", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
                db.Add(new IdentityPatients.Domain.Entities.Staff { Id = fixture.StaffId, AppUserId = fixture.UserId,
                    StaffCode = "TEST-" + fixture.StaffId.ToString("N"), FullName = "Test Nurse",
                    StaffType = IdentityPatients.Domain.Enums.StaffType.Nurse, IsActive = true, CreatedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync(Ct);
            }
            Authorize(client, app.Configuration, fixture.UserId);
            client.DefaultRequestHeaders.Add("Idempotency-Key", "real-identity");
            using var saved = await client.PostAsJsonAsync(Route(fixture.EncounterId), ClinicalVitalsFixture.Request, Ct);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            Assert.Equal(fixture.StaffId, (await saved.Content.ReadFromJsonAsync<RecordedVitals>(Ct))!.VitalSign.RecordedByStaffId);
            await using var scope = app.Services.CreateAsyncScope();
            var identity = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            foreach (var (type, staffActive, userActive) in new[] {
                (IdentityPatients.Domain.Enums.StaffType.Doctor, true, true),
                (IdentityPatients.Domain.Enums.StaffType.Nurse, false, true),
                (IdentityPatients.Domain.Enums.StaffType.Nurse, true, false) })
            {
                await identity.Staffs.Where(x => x.Id == fixture.StaffId).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.StaffType, type).SetProperty(x => x.IsActive, staffActive), Ct);
                await identity.AppUsers.Where(x => x.Id == fixture.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, userActive), Ct);
                using var forbidden = await client.PostAsJsonAsync(Route(fixture.EncounterId), ClinicalVitalsFixture.Request, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            }
            Assert.Equal((1, 1, 1), await fixture.CountsAsync());
        }
        finally
        {
            await using var cleanup = app.Services.CreateAsyncScope();
            var db = cleanup.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            await db.Staffs.Where(x => x.Id == fixture.StaffId).ExecuteDeleteAsync(Ct);
            await db.AppUsers.Where(x => x.Id == fixture.UserId).ExecuteDeleteAsync(Ct);
        }
    }

    private static async Task<WebApplication> App(Directory? directory, bool prototype = true)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Vitals.Tests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
            .AddClinicalInfrastructure(cs).AddUnitOfWorkParticipant<ClinicalDbContext>().AddClinicalApi();
        if (directory is not null) builder.Services.AddSingleton<INurseStaffDirectory>(directory);
        var app = builder.Build();
        if (prototype) app.MapGroup("/api/clinical").MapNurseVitalsRoutes();
        else app.MapClinicalEndpoints();
        await app.StartAsync(Ct);
        return app;
    }

    private static string Route(Guid id) => $"/api/clinical/staff/nurse/encounters/{id}/vitals";
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains($"\"code\":\"{code}\"", await response.Content.ReadAsStringAsync(Ct));
    }
    private static void Authorize(HttpClient client, IConfiguration config, Guid user, string role = Roles.Nurse, string channel = Channels.Staff)
    {
        var token = new JwtSecurityToken("Vitals.Tests", "Vitals.Tests",
            [new("sub", user.ToString()), new("role", role), new(AppClaims.Channel, channel)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
    private sealed class Directory(Guid staffId) : INurseStaffDirectory
    {
        public Guid? StaffId { get; set; } = staffId;
        public Guid? LastUserId { get; private set; }
        public Task<Guid?> GetActiveNurseStaffIdAsync(Guid appUserId, CancellationToken ct)
        {
            LastUserId = appUserId;
            return Task.FromResult(StaffId);
        }
    }
}
