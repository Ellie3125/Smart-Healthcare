using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Clinical.Api;
using Clinical.Application.Results;
using Clinical.Contracts;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Infrastructure;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalClsPublicationEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Routes_require_room_staff_and_validate_claims_json_key_and_version()
    {
        await using var f = await ClinicalClsPublicationTests.CreateAsync(new());
        await using var app = await AppAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var route = Route(f.ItemId);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Post })
        {
            var url = method == HttpMethod.Post ? route + "/publish" : route;
            using var anonymous = await client.SendAsync(new(method, url), Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.RoomAccount, Channels.Patient),
                (Roles.Doctor, Channels.Staff), (Roles.Nurse, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Receptionist, Channels.Staff) })
            {
                Authorize(client, app.Configuration, f.UserId, f.RoomId, role, channel);
                using var denied = await client.SendAsync(new(method, url), Ct);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
            client.DefaultRequestHeaders.Authorization = null;
        }
        Authorize(client, app.Configuration, f.UserId, null);
        using var missingRoom = await client.GetAsync(route, Ct); Assert.Equal(HttpStatusCode.Forbidden, missingRoom.StatusCode);
        Authorize(client, app.Configuration, f.UserId, Guid.NewGuid());
        using var wrongRoom = await client.GetAsync(route, Ct); Assert.Equal(HttpStatusCode.Forbidden, wrongRoom.StatusCode);
        Authorize(client, app.Configuration, f.UserId, f.RoomId);
        using var media = await client.PostAsync(route + "/publish", new StringContent("plain"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, media.StatusCode);
        using var noKey = await client.PostAsJsonAsync(route + "/publish", new { version = "AAAAAAAAAAA=" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "http-errors");
        foreach (var json in new[] { "{", "null", "{\"version\":1}", "{\"version\":\"bad\"}" })
        {
            using var invalid = await client.PostAsync(route + "/publish", new StringContent(json, Encoding.UTF8, "application/json"), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", new[] { "a", "b" });
        using var duplicate = await client.PostAsJsonAsync(route + "/publish", new { version = "AAAAAAAAAAA=" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Production_route_publishes_with_real_room_session_and_identity_doctor_shift_and_room_cannot_read_it_afterwards()
    {
        await using var f = await ClinicalClsPublicationTests.CreateAsync(new());
        await using var app = await AppAsync(realReader: true);
        var rawSession = Guid.NewGuid().ToString("N");
        var doctor = Guid.NewGuid(); var staff = Guid.NewGuid(); var doctorUser = Guid.NewGuid();
        var specialty = Guid.NewGuid(); var rank = Guid.NewGuid(); var account = Guid.NewGuid();
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>(); var now = DateTime.UtcNow;
                db.AddRange(new AppUser { Id = f.UserId, UserName = f.UserId.ToString("N"), PasswordHash = "test", IsActive = true, CreatedAtUtc = now },
                    new AppUser { Id = doctorUser, UserName = doctorUser.ToString("N"), PasswordHash = "test", IsActive = true, CreatedAtUtc = now },
                    new Room { Id = f.RoomId, RoomNumber = f.RoomId.ToString("N"), RoomName = "Test", DepartmentName = "Test", RoomType = RoomType.Laboratory, IsActive = true },
                    new RoomAccount { Id = account, AppUserId = f.UserId, RoomId = f.RoomId, AccountCode = account.ToString("N"), IsActive = true, CreatedAtUtc = now },
                    new RoomDeviceSession { Id = Guid.NewGuid(), RoomAccountId = account, DeviceId = "test-device", DeviceIpAddress = "127.0.0.1", IsActive = true, LoginAtUtc = now,
                        SessionToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSession))).ToLowerInvariant() },
                    new Specialty { Id = specialty, SpecialtyCode = specialty.ToString("N"), SpecialtyName = "Test", IsActive = true },
                    new DoctorRank { Id = rank, RankCode = RankCode.SpecialistI, RankName = "Test", IsActive = true },
                    new Staff { Id = staff, AppUserId = doctorUser, StaffCode = staff.ToString("N"), FullName = "Bác sĩ ca trực giả lập", StaffType = StaffType.Doctor, IsActive = true, CreatedAtUtc = now },
                    new Doctor { Id = doctor, StaffId = staff, SpecialtyId = specialty, DoctorRankId = rank, IsActive = true },
                    new StaffShiftAssignment { Id = Guid.NewGuid(), StaffId = staff, RoomId = f.RoomId, ShiftDate = DateOnly.FromDateTime(now.AddHours(7)),
                        ShiftName = ShiftName.FullDay, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1) });
                await db.SaveChangesAsync(Ct);
            }
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) }; var route = Route(f.ItemId);
            Authorize(client, app.Configuration, f.UserId, f.RoomId);
            using var noSession = await client.GetAsync(route, Ct); Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
            Authorize(client, app.Configuration, f.UserId, f.RoomId, session: rawSession);
            var draft = await client.GetFromJsonAsync<ClsResultDraft>(route, Ct); Assert.NotNull(draft);
            Assert.Equal(doctor, Assert.Single(draft.ReadingDoctors).DoctorId);
            using var saved = await client.PutAsJsonAsync(route, new ClsDraftRequest("Tóm tắt HTTP", "Chi tiết HTTP", draft.Version), Ct);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var updated = await saved.Content.ReadFromJsonAsync<ClsResultDraft>(Ct); Assert.NotNull(updated);
            client.DefaultRequestHeaders.Add("Idempotency-Key", "http-publish");
            using var sent = await client.PostAsJsonAsync(route + "/publish", new ClsPublishRequest(updated.Version), Ct);
            Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
            Assert.DoesNotContain("Tóm tắt", await sent.Content.ReadAsStringAsync(Ct));
            using (var receipt = System.Text.Json.JsonDocument.Parse(await sent.Content.ReadAsStringAsync(Ct)))
                Assert.Equal(new[] { "orderItemId", "publishedAtUtc", "resultId", "resultStatus", "version" },
                    receipt.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
            using var hidden = await client.GetAsync(route, Ct); Assert.Equal(HttpStatusCode.Conflict, hidden.StatusCode);
            var legacy = await client.GetFromJsonAsync<IReadOnlyList<Clinical.Contracts.ServiceOrderDto>>(
                $"/api/clinical/staff/room/encounters/{f.EncounterId}/service-orders", Ct);
            Assert.NotNull(legacy); Assert.All(legacy.SelectMany(x => x.Items), i => Assert.Null(i.Result));
            using var replay = await client.PostAsJsonAsync(route + "/publish", new ClsPublishRequest(updated.Version), Ct);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            await using var check = app.Services.CreateAsyncScope();
            var result = await check.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Clinical.Domain.Entities.CLSResult>()
                .SingleAsync(x => x.ServiceOrderItemId == f.ItemId, Ct);
            Assert.Equal(staff, result.ApprovedByStaffId);
        }
        finally
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            await db.StaffShiftAssignments.Where(x => x.RoomId == f.RoomId).ExecuteDeleteAsync(Ct);
            await db.RoomDeviceSessions.Where(x => x.RoomAccountId == account).ExecuteDeleteAsync(Ct);
            await db.RoomAccounts.Where(x => x.Id == account).ExecuteDeleteAsync(Ct);
            await db.Doctors.Where(x => x.Id == doctor).ExecuteDeleteAsync(Ct);
            await db.Staffs.Where(x => x.Id == staff).ExecuteDeleteAsync(Ct);
            await db.Specialties.Where(x => x.Id == specialty).ExecuteDeleteAsync(Ct);
            await db.DoctorRanks.Where(x => x.Id == rank).ExecuteDeleteAsync(Ct);
            await db.Rooms.Where(x => x.Id == f.RoomId).ExecuteDeleteAsync(Ct);
            await db.AppUsers.Where(x => x.Id == f.UserId || x.Id == doctorUser).ExecuteDeleteAsync(Ct);
        }
    }

    private static string Route(Guid item) => $"/api/clinical/staff/room/results/{item}";
    [Fact]
    public async Task Legacy_reads_hide_drafts_and_limit_published_text_to_original_doctor_or_verified_patient_after_conclusion()
    {
        var directory = new ClinicalClsPublicationTests.ReadingDirectory();
        await using var f = await ClinicalClsPublicationTests.CreateAsync(directory);
        var draft = await ClinicalClsPublicationTests.SaveAsync(f);
        await ClinicalClsPublicationTests.PublishAsync(f, new(draft.Version), "privacy");
        await using var app = await AppAsync(directory: directory);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var staffRoute = $"/api/clinical/staff/doctor/encounters/{f.EncounterId}/service-orders";
        Authorize(client, app.Configuration, f.UserId, f.RoomId, Roles.Doctor, Channels.Staff);
        using var noDoctor = await client.GetAsync(staffRoute, Ct); Assert.Equal(HttpStatusCode.Forbidden, noDoctor.StatusCode);
        Authorize(client, app.Configuration, f.UserId, f.RoomId, Roles.Doctor, Channels.Staff, doctor: Guid.NewGuid());
        using var wrongDoctor = await client.GetAsync(staffRoute, Ct); Assert.Equal(HttpStatusCode.Forbidden, wrongDoctor.StatusCode);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var encounter = await db.Set<Clinical.Domain.Entities.Encounter>().SingleAsync(x => x.Id == f.EncounterId, Ct);
        Authorize(client, app.Configuration, f.UserId, f.RoomId, Roles.Doctor, Channels.Staff, doctor: encounter.OriginalDoctorId);
        Assert.Equal("Tóm tắt giả lập", Assert.Single(Assert.Single((await client.GetFromJsonAsync<IReadOnlyList<ServiceOrderDto>>(staffRoute, Ct))!).Items).Result!.ResultSummary);
        Authorize(client, app.Configuration, f.UserId, f.RoomId, Roles.Nurse, Channels.Staff);
        var nurseOrders = await client.GetFromJsonAsync<IReadOnlyList<ServiceOrderDto>>(staffRoute.Replace("/doctor/", "/nurse/"), Ct);
        Assert.Null(Assert.Single(Assert.Single(nurseOrders!).Items).Result);
        Authorize(client, app.Configuration, f.UserId, Guid.NewGuid());
        Assert.Empty((await client.GetFromJsonAsync<IReadOnlyList<ServiceOrderDto>>(staffRoute.Replace("/doctor/", "/room/"), Ct))!);
        var patientRoute = $"/api/clinical/patient/encounters/{f.EncounterId}/service-orders";
        Authorize(client, app.Configuration, f.UserId, null, Roles.Patient, Channels.Patient);
        using var noAccess = await client.GetAsync(patientRoute, Ct); Assert.Equal(HttpStatusCode.NotFound, noAccess.StatusCode);
        directory.AllowPatient = true;
        async Task<CLSResultDto?> PatientResult()
        {
            var orders = await client.GetFromJsonAsync<IReadOnlyList<ServiceOrderDto>>(patientRoute, Ct);
            return Assert.Single(Assert.Single(orders!).Items).Result;
        }
        Assert.Null(await PatientResult());
        var record = new Clinical.Domain.Entities.EncounterClinicalRecord { Id = Guid.NewGuid(), EncounterId = f.EncounterId,
            Status = Clinical.Domain.Enums.EncounterClinicalRecordStatus.Draft, PostCLSConclusion = "draft", UpdatedByDoctorId = encounter.OriginalDoctorId,
            RecordedAtUtc = DateTime.UtcNow, LastUpdatedUtc = DateTime.UtcNow };
        db.Add(record); await db.SaveChangesAsync(Ct); Assert.Null(await PatientResult());
        record.Status = Clinical.Domain.Enums.EncounterClinicalRecordStatus.Published; record.PostCLSConclusion = " ";
        await db.SaveChangesAsync(Ct); Assert.Null(await PatientResult());
        record.PostCLSConclusion = "Kết luận giả lập"; record.PublishedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(Ct);
        Assert.Equal("Tóm tắt giả lập", (await PatientResult())!.ResultSummary);
        await db.Set<Clinical.Domain.Entities.CLSResult>().Where(x => x.Id == draft.ResultId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ResultStatus, Clinical.Domain.Enums.ResultStatus.Cancelled), Ct);
        Assert.Null(await PatientResult());
    }

    private static async Task<WebApplication> AppAsync(bool realReader = false, ClinicalClsPublicationTests.ReadingDirectory? directory = null)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "ClsPublication.Tests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
            .AddClinicalInfrastructure(cs).AddSchedulingInfrastructure(cs).AddClinicalApi().AddUnitOfWorkParticipant<ClinicalDbContext>();
        ClinicalClsPublicationTests.Configure(builder.Services, directory ?? new());
        if (realReader) builder.Services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();
        var app = builder.Build(); app.MapClinicalEndpoints(); await app.StartAsync(Ct); return app;
    }
    private static void Authorize(HttpClient client, IConfiguration config, Guid user, Guid? room,
        string role = Roles.RoomAccount, string channel = Channels.Staff, string? session = null, Guid? doctor = null)
    {
        var claims = new List<System.Security.Claims.Claim> { new("sub", user.ToString()), new("role", role), new(AppClaims.Channel, channel) };
        if (room is { } id) claims.Add(new(AppClaims.RoomId, id.ToString()));
        if (session is not null) claims.Add(new("room_session", session));
        if (doctor is { } doctorId) claims.Add(new(AppClaims.DoctorId, doctorId.ToString()));
        var token = new JwtSecurityToken("ClsPublication.Tests", "ClsPublication.Tests", claims, expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}
