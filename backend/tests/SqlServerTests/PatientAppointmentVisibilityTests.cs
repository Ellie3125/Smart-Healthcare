using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Infrastructure;
using Clinical.Infrastructure;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Infrastructure;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Application.Appointments;
using Scheduling.Domain.Entities;
using Scheduling.Contracts;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class PatientAppointmentVisibilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Route = "/api/scheduling/patient/appointments";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Counter_store_guard_rechecks_channel_under_lock_and_does_not_write(bool reschedule)
    {
        await using var f = await Host.CreateAsync();
        var id = await f.AppointmentAsync(f.Patient, f.Actor, "Counter");
        var targetId = await f.AppointmentAsync(f.Patient, f.Actor, "Web");
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var target = await db.Set<Appointment>().SingleAsync(a => a.Id == targetId, Ct);
        var quota = await db.Set<DoctorDayQuota>().SingleAsync(q => q.Id == target.SessionQuotaId, Ct);
        var store = scope.ServiceProvider.GetRequiredService<IAppointmentChangeStore>();
        var outcome = reschedule
            ? await store.RescheduleAsync(new(id, f.Actor, f.Patient, quota.DoctorId, Guid.NewGuid(), quota.ServiceDate, null, "counter"), 0, Ct)
            : await store.CancelAsync(new(id, f.Actor, CancellationInitiator.Patient, f.Patient, null, null, "counter"), Guid.NewGuid(), Ct);
        Assert.Equal("CounterAppointmentRequiresReception", outcome.Result.ToString());
        Assert.Equal("Scheduled", Assert.Single(await f.Db.QueryAsync($"SELECT Status FROM scheduling.Appointment WHERE Id='{id}'")));
        Assert.Equal(2, await db.Set<Appointment>().CountAsync(a => a.PatientId == f.Patient, Ct));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Verified")]
    public async Task Booking_profiles_include_counter_and_online_appointments_with_acl_before_paging(string verification)
    {
        await using var f = await Host.CreateAsync(verification);
        var own = await f.AppointmentAsync(f.Patient, f.Actor, "Web");
        var walkIn = await f.AppointmentAsync(f.Patient, f.Receptionist, "Counter");
        var relative = await f.AppointmentAsync(f.Relative, f.Receptionist, "Counter");
        var foreign = await f.AppointmentAsync(f.Foreign, f.Receptionist, "Counter");
        using var client = f.Client();
        var page = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Equal(new[] { own, walkIn, relative }.Order(), page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("appointmentId").GetGuid()).Order());
        var first = await client.GetFromJsonAsync<JsonElement>(Route + "?pageSize=2", Ct);
        var second = await client.GetFromJsonAsync<JsonElement>(Route + "?pageSize=2&page=2", Ct);
        Assert.Equal(2, first.GetProperty("items").GetArrayLength()); Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Single(second.GetProperty("items").EnumerateArray()); Assert.False(second.GetProperty("hasMore").GetBoolean());
        using var detail = await client.GetAsync(Route + "/" + walkIn, Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var denied = await client.GetAsync(Route + "/" + foreign, Ct);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await f.Db.QueryAsync($"UPDATE [identity].PatientRepresentative SET IsActive=0 WHERE AppUserId='{f.Actor}'");
        var empty = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        using var revoked = await client.GetAsync(Route + "/" + walkIn, Ct);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    [Theory]
    [InlineData("Scheduled")]
    [InlineData("CheckedIn")]
    public async Task Counter_appointments_cannot_be_cancelled_or_rescheduled_online(string status)
    {
        await using var f = await Host.CreateAsync();
        var id = await f.AppointmentAsync(f.Patient, f.Receptionist, "Counter", status);
        using var client = f.Client();
        foreach (var action in new[] { "cancel", "reschedule", "cancellation-preview" })
        {
            using var request = new HttpRequestMessage(action == "cancellation-preview" ? HttpMethod.Get : HttpMethod.Post, Route + "/" + id + "/" + action);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            if (action == "reschedule") request.Content = JsonContent.Create(new { targetDoctorId = Guid.NewGuid(), targetServiceDate = "2088-02-04" });
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("CounterAppointmentRequiresReception", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
        Assert.Equal(status, Assert.Single(await f.Db.QueryAsync($"SELECT Status FROM scheduling.Appointment WHERE Id='{id}'")));
    }

    [Fact]
    public async Task Another_representatives_online_booking_is_readable_but_mutation_keeps_the_original_owner_rule()
    {
        await using var f = await Host.CreateAsync();
        var id = await f.AppointmentAsync(f.Patient, f.Receptionist, "Web");
        using var client = f.Client();
        using var detail = await client.GetAsync(Route + "/" + id, Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route + "/" + id + "/cancel");
        request.Headers.Add("Idempotency-Key", "keep-owner");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class Host(WebApplication app, TestDatabase db, Guid actor, Guid receptionist, Guid patient, Guid relative, Guid foreign) : IAsyncDisposable
    {
        public TestDatabase Db => db;
        public IServiceProvider Services => app.Services;
        public Guid Actor => actor;
        public Guid Receptionist => receptionist;
        public Guid Patient => patient;
        public Guid Relative => relative;
        public Guid Foreign => foreign;
        public static async Task<Host> CreateAsync(string verification = "Verified")
        {
            var db = await TestDatabase.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "AppointmentVisibility.Tests";
            var cs = TestDatabase.RequiredConnectionString();
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
                .AddSchedulingInfrastructure(cs).AddSchedulingApplication().AddSchedulingApi()
                .AddNotificationsInfrastructure(cs).AddClinicalInfrastructure(cs).AddBillingInfrastructure(cs);
            builder.Services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<SchedulingDbContext>(o => new(o)));
            var app = builder.Build(); app.MapSchedulingEndpoints(); await app.StartAsync(Ct);
            var actor = await db.InsertAsync("identity.AppUser", new { IsActive = true });
            var receptionist = await db.InsertAsync("identity.AppUser", new { IsActive = true });
            var patient = await db.InsertAsync("identity.Patient", new { Gender = "Male" });
            var relative = await db.InsertAsync("identity.Patient", new { Gender = "Female" });
            var foreign = await db.InsertAsync("identity.Patient", new { Gender = "Male" });
            await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = actor, PatientId = patient, Relationship = "Self", VerificationStatus = verification, IsActive = true });
            await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = actor, PatientId = relative, Relationship = "Parent", VerificationStatus = verification, IsActive = true });
            return new(app, db, actor, receptionist, patient, relative, foreign);
        }
        public async Task<Guid> AppointmentAsync(Guid profile, Guid bookedBy, string channel, string status = "Scheduled")
        {
            var quota = await db.InsertAsync("scheduling.DoctorDayQuota", new { ServiceDate = new DateTime(2088, 2, 4), SessionStatus = "Scheduled", IsOpen = true, Capacity = 30, AdmittedCount = 1 });
            var attempt = await db.InsertAsync("scheduling.BookingAttempt", new { AppUserId = bookedBy, PatientId = profile, Status = "Confirmed" });
            var reservation = await db.InsertAsync("scheduling.Reservation", new { AppUserId = bookedBy, PatientId = profile, BookingAttemptId = attempt, SessionQuotaId = quota, BookingChannel = channel, Status = "Confirmed" });
            return await db.InsertAsync("scheduling.Appointment", new { PatientId = profile, SessionQuotaId = quota, ReservationId = reservation, Status = status });
        }
        public HttpClient Client()
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            var token = new JwtSecurityToken("AppointmentVisibility.Tests", "AppointmentVisibility.Tests", [new("sub", actor.ToString()), new("role", Roles.Patient), new(AppClaims.Channel, Channels.Patient)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(Ct); await app.DisposeAsync(); await db.DisposeAsync(); }
    }
}
