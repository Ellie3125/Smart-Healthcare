using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Clinical.Api;
using Clinical.Application;
using Clinical.Contracts;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

public sealed class PatientJourneyQueueContextTests
{
    private const string Route = "/api/clinical/patient/today-journey";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Journey_requires_patient_channel_and_valid_profile_query()
    {
        await using var f = await JourneyHost.CreateAsync(); using var anonymous = f.Client(authenticate: false);
        using var denied = await anonymous.GetAsync(Route, Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Doctor, Channels.Staff),
            (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            using var wrong = f.Client(role, channel); using var response = await wrong.GetAsync(Route, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var client = f.Client();
        foreach (var query in new[] { "?patientProfileId=bad", "?patientProfileId=", "?patientProfileId=" + Guid.Empty, "?patientProfileId=a&patientProfileId=b" })
        { using var response = await client.GetAsync(Route + query, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
    }

    [Fact]
    public async Task Self_default_is_empty_without_a_visit_and_does_not_select_a_relative()
    {
        await using var f = await JourneyHost.CreateAsync(); using var client = f.Client();
        var empty = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Equal(f.Patient, empty.GetProperty("patientProfileId").GetGuid());
        Assert.Equal("2087-02-04", empty.GetProperty("serviceDate").GetString());
        Assert.Empty(empty.GetProperty("steps").EnumerateArray()); Assert.Equal(0, empty.GetProperty("unpaidClsCount").GetInt32());
        await f.Db.InsertAsync("identity.PatientRepresentative", new { AppUserId = f.Actor, PatientId = f.OtherPatient,
            Relationship = "Parent", VerificationStatus = "Verified", IsActive = true });
        await f.Db.QueryAsync($"UPDATE [identity].PatientRepresentative SET IsActive=0 WHERE Id='{f.SelfLink}'");
        using var missing = await client.GetAsync(Route, Ct); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var relative = await client.GetAsync(Route + "?patientProfileId=" + f.OtherPatient, Ct); Assert.Equal(HttpStatusCode.OK, relative.StatusCode);
    }

    [Theory]
    [InlineData("VerificationStatus=N'Pending'")]
    [InlineData("IsActive=0")]
    [InlineData("VerificationStatus=N'Rejected'")]
    [InlineData("VerificationStatus=N'Revoked'")]
    public async Task Unverified_or_revoked_link_cannot_read_journey(string change)
    {
        await using var f = await JourneyHost.CreateAsync(); using var client = f.Client();
        await f.Db.QueryAsync($"UPDATE [identity].PatientRepresentative SET {change} WHERE Id='{f.SelfLink}'");
        using var response = await client.GetAsync(Route + "?patientProfileId=" + f.Patient, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_foreign_or_disabled_account_and_ambiguous_self_are_not_disclosed()
    {
        await using var f = await JourneyHost.CreateAsync(); using var client = f.Client();
        foreach (var patient in new[] { f.OtherPatient, Guid.NewGuid() })
        { using var response = await client.GetAsync(Route + "?patientProfileId=" + patient, Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        await f.Db.InsertAsync("identity.PatientRepresentative", new { AppUserId = f.Actor, PatientId = f.OtherPatient,
            Relationship = "Self", VerificationStatus = "Verified", IsActive = true });
        using var ambiguous = await client.GetAsync(Route, Ct); Assert.Equal(HttpStatusCode.NotFound, ambiguous.StatusCode);
        await f.Db.QueryAsync($"UPDATE [identity].AppUser SET IsActive=0 WHERE Id='{f.Actor}'");
        using var inactive = await client.GetAsync(Route + "?patientProfileId=" + f.Patient, Ct); Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
    }

    [Fact]
    public async Task Before_checkin_journey_uses_own_today_appointment_and_real_room()
    {
        await using var f = await JourneyHost.CreateAsync(); var appointment = await f.AppointmentAsync(); using var client = f.Client();
        var result = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Equal(appointment, result.GetProperty("appointmentId").GetGuid());
        var step = Assert.Single(result.GetProperty("steps").EnumerateArray());
        Assert.Equal("Waiting", step.GetProperty("status").GetString()); Assert.Equal("Clinical test room", step.GetProperty("location").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("encounterStatus").ValueKind);
    }

    [Fact]
    public async Task Journey_composes_today_queues_and_actual_unpaid_CLS_without_medical_payloads()
    {
        await using var f = await JourneyHost.CreateAsync(); var encounter = await f.EncounterAsync();
        var item = Guid.NewGuid();
        await f.TicketAsync(encounter, f.Room, "InitialConsultation", "Finished", 1, f.Now.AddMinutes(-20));
        var cls = await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Called", 2, f.Now.AddMinutes(-10), item);
        await f.TicketAsync(encounter, f.Room, "ReturnConsultation", "Deferred", 3, f.Now.AddMinutes(-5));
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Cancelled", 4, f.Now.AddMinutes(-1));
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Waiting", 5, f.Now.AddDays(-1));
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Waiting", 6, f.Now.AddDays(1));
        await f.ChargeAsync(encounter, item, BillingItemStatus.Pending, 100, 0);
        await f.ChargeAsync(encounter, Guid.NewGuid(), BillingItemStatus.PartiallyPaid, 100, 50);
        await f.ChargeAsync(encounter, Guid.NewGuid(), BillingItemStatus.Paid, 100, 100);
        await f.ChargeAsync(encounter, Guid.NewGuid(), BillingItemStatus.Cancelled, 100, 0);
        await f.ChargeAsync(encounter, Guid.NewGuid(), BillingItemStatus.Waived, 100, 0);
        await f.ChargeAsync(encounter, Guid.NewGuid(), BillingItemStatus.Pending, 0, 0);
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Equal("WaitingCLS", result.GetProperty("encounterStatus").GetString()); Assert.Equal(2, result.GetProperty("unpaidClsCount").GetInt32());
        var steps = result.GetProperty("steps").EnumerateArray().ToArray(); Assert.Equal(3, steps.Length);
        Assert.Equal(new[] { "Completed", "Called", "Deferred" }, steps.Select(s => s.GetProperty("status").GetString()));
        Assert.Equal(cls.ToString(), steps[1].GetProperty("id").GetString());
        Assert.Equal("CLS test room", steps[1].GetProperty("location").GetString());
        Assert.Contains("T-2", result.GetProperty("currentCallText").GetString());
        Assert.DoesNotContain("diagnosis", result.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conclusion", result.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Today_selection_uses_GMT7_bounds_and_prefers_an_active_encounter()
    {
        await using var f = await JourneyHost.CreateAsync();
        var start = new DateTime(2087, 2, 3, 17, 0, 0, DateTimeKind.Utc);
        await f.EncounterAsync(at: start.AddTicks(-1), status: "Completed");
        var active = await f.EncounterAsync(at: start); await f.TicketAsync(active, f.Room, "InitialConsultation", "InProgress", 1, start);
        await f.EncounterAsync(at: f.Now.AddMinutes(5), status: "Completed");
        await f.EncounterAsync(at: start.AddDays(1), status: "Completed");
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        Assert.Equal("WaitingCLS", result.GetProperty("encounterStatus").GetString());
        Assert.Single(result.GetProperty("steps").EnumerateArray());
    }

    [Fact]
    public async Task A_return_ticket_issued_yesterday_but_activated_today_is_in_the_journey()
    {
        await using var f = await JourneyHost.CreateAsync(); var encounter = await f.EncounterAsync(at: f.Now.AddDays(-1));
        await f.Db.InsertAsync("scheduling.QueueTicket", new { EncounterId = encounter, TargetRoomId = f.Room, TicketType = "ReturnConsultation",
            QueueStatus = "Waiting", TicketNumber = "R-1", SequenceNumber = 1, IssuedAtUtc = f.Now.AddDays(-1), ActivatedAtUtc = f.Now, ServiceOrderId = Guid.NewGuid() });
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Route, Ct);
        var step = Assert.Single(result.GetProperty("steps").EnumerateArray()); Assert.Equal("R-1", step.GetProperty("queueNumber").GetString());
    }

    [Fact]
    public async Task Room_context_reader_rejects_invalid_batches()
    {
        await using var f = await JourneyHost.CreateAsync();
        await using var scope = f.App.Services.CreateAsyncScope(); var clinical = scope.ServiceProvider.GetRequiredService<IClinicalModule>();
        await Assert.ThrowsAsync<ClinicalException>(() => clinical.GetQueuePatientContextsAsync([Guid.Empty], Ct));
        await Assert.ThrowsAsync<ClinicalException>(() => clinical.GetQueuePatientContextsAsync(Enumerable.Repeat(Guid.NewGuid(), 101).ToArray(), Ct));
        Assert.Empty(await clinical.GetQueuePatientContextsAsync([], Ct));
    }

    [Fact]
    public async Task Existing_staff_queue_contains_encounter_and_patient_context_for_doctor_and_CLS_room()
    {
        await using var f = await JourneyHost.CreateAsync(); var encounter = await f.EncounterAsync();
        await f.TicketAsync(encounter, f.Room, "InitialConsultation", "Waiting", 1, f.Now.AddMinutes(-5));
        await f.TicketAsync(encounter, f.Room, "ReturnConsultation", "Deferred", 2, f.Now.AddMinutes(-4));
        var orderItem = Guid.NewGuid(); await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Called", 3, f.Now.AddMinutes(-3), orderItem);
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Waiting", 4, f.Now.AddDays(-1));
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Waiting", 5, f.Now.AddDays(1));
        await f.TicketAsync(encounter, f.ClsRoom, "CLS", "Finished", 6, f.Now);
        foreach (var (role, room) in new[] { (Roles.Doctor, f.Room), (Roles.RoomAccount, f.ClsRoom) })
        {
            using var client = f.Client(role, Channels.Staff, room: room); var result = await client.GetFromJsonAsync<JsonElement>($"/api/scheduling/staff/rooms/{room}/queue", Ct);
            Assert.Equal(role == Roles.Doctor ? 2 : 1, result.GetProperty("items").GetArrayLength());
            foreach (var row in result.GetProperty("items").EnumerateArray())
            { Assert.Equal(encounter, row.GetProperty("encounterId").GetGuid()); Assert.Equal("Journey test patient", row.GetProperty("patientName").GetString()); }
            if (role == Roles.RoomAccount) Assert.Equal(orderItem, result.GetProperty("items")[0].GetProperty("serviceOrderItemId").GetGuid());
        }
        using var publicClient = f.Client(authenticate: false); var display = await publicClient.GetFromJsonAsync<JsonElement>($"/api/scheduling/public/display?roomId={f.ClsRoom}", Ct);
        Assert.DoesNotContain("patient", display.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(encounter.ToString(), display.GetRawText());
    }

    [Fact]
    public async Task Nurse_room_shift_and_doctor_or_room_assignment_are_enforced_before_names()
    {
        await using var f = await JourneyHost.CreateAsync(); var encounter = await f.EncounterAsync();
        await f.TicketAsync(encounter, f.Room, "InitialConsultation", "Waiting", 1, f.Now);
        using var nurse = f.Client(Roles.Nurse, Channels.Staff);
        using var unassigned = await nurse.GetAsync($"/api/scheduling/staff/rooms/{f.Room}/queue", Ct); Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        await f.NurseDutyAsync();
        using var assigned = await nurse.GetAsync($"/api/scheduling/staff/rooms/{f.Room}/queue", Ct); Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var wrongRoom = await nurse.GetAsync($"/api/scheduling/staff/rooms/{f.ClsRoom}/queue", Ct); Assert.Equal(HttpStatusCode.Forbidden, wrongRoom.StatusCode);
        await f.Db.QueryAsync($"UPDATE [identity].StaffShiftAssignment SET EndsAtUtc='2087-02-03T17:29:00' WHERE StaffId='{f.NurseStaff}'");
        using var ended = await nurse.GetAsync($"/api/scheduling/staff/rooms/{f.Room}/queue", Ct); Assert.Equal(HttpStatusCode.Forbidden, ended.StatusCode);
        using var doctor = f.Client(Roles.Doctor, Channels.Staff); using var docDenied = await doctor.GetAsync($"/api/scheduling/staff/rooms/{f.ClsRoom}/queue", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, docDenied.StatusCode);
        using var room = f.Client(Roles.RoomAccount, Channels.Staff, room: f.Room); using var roomDenied = await room.GetAsync($"/api/scheduling/staff/rooms/{f.ClsRoom}/queue", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, roomDenied.StatusCode);
    }

    private sealed class JourneyHost(WebApplication app, TestDatabase db, Guid actor, Guid patient, Guid otherPatient, Guid link, Guid room, Guid clsRoom) : IAsyncDisposable
    {
        public TestDatabase Db => db;
        public WebApplication App => app;
        public Guid Actor => actor; public Guid Patient => patient; public Guid OtherPatient => otherPatient; public Guid SelfLink => link;
        public Guid Room => room; public Guid ClsRoom => clsRoom; public Guid Doctor { get; } = Guid.NewGuid(); public Guid NurseStaff { get; } = Guid.NewGuid();
        public DateTime Now { get; } = new(2087, 2, 3, 17, 30, 0, DateTimeKind.Utc);
        private Guid PriceList { get; } = Guid.NewGuid(); private Guid PriceItem { get; } = Guid.NewGuid();
        private Guid Quota { get; set; }
        public static async Task<JourneyHost> CreateAsync()
        {
            var db = await TestDatabase.OpenAsync(); var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Journey.Tests";
            var cs = TestDatabase.RequiredConnectionString();
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
                .AddSchedulingInfrastructure(cs).AddSchedulingApplication().AddSchedulingApi()
                .AddClinicalInfrastructure(cs).AddClinicalApplication().AddClinicalApi().AddBillingInfrastructure(cs).AddBillingApplication();
            builder.Services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<SharedDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<SchedulingDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<ClinicalDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => db.CreateContext<BillingDbContext>(o => new(o)));
            builder.Services.AddSingleton<IDbClock>(new FixedClock());
            var app = builder.Build(); app.MapClinicalEndpoints(); app.MapSchedulingRoomQueueEndpoints(); await app.StartAsync(Ct);
            var actor = await db.InsertAsync("identity.AppUser", new { UserName = "journey-" + Guid.NewGuid().ToString("N"), IsActive = true });
            var patient = await db.InsertAsync("identity.Patient", new { FullName = "Journey test patient", Gender = "Male" });
            var other = await db.InsertAsync("identity.Patient", new { FullName = "Other test patient", Gender = "Female" });
            var link = await db.InsertAsync("identity.PatientRepresentative", new { AppUserId = actor, PatientId = patient, Relationship = "Self", VerificationStatus = "Verified", IsActive = true });
            var room = await db.InsertAsync("identity.Room", new { RoomType = "Clinical", RoomName = "Clinical test room", IsActive = true });
            var clsRoom = await db.InsertAsync("identity.Room", new { RoomType = "Laboratory", RoomName = "CLS test room", IsActive = true });
            var host = new JourneyHost(app, db, actor, patient, other, link, room, clsRoom);
            host.Quota = await db.InsertAsync("scheduling.DoctorDayQuota", new { DoctorId = host.Doctor, AssignedRoomId = room, ServiceDate = new DateTime(2087, 2, 4), SessionStatus = "Scheduled", IsOpen = true });
            return host;
        }
        public HttpClient Client(string role = Roles.Patient, string channel = Channels.Patient, bool authenticate = true, Guid? room = null)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            if (authenticate)
            {
                var token = new JwtSecurityToken("Journey.Tests", "Journey.Tests", [new("sub", Actor.ToString()), new("role", role), new(AppClaims.Channel, channel),
                    new(AppClaims.DoctorId, Doctor.ToString()), new(AppClaims.RoomId, (room ?? Room).ToString())], expires: DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }
        public async Task<Guid> AppointmentAsync(string status = "Scheduled")
        {
            var attempt = await db.InsertAsync("scheduling.BookingAttempt", new { AppUserId = Actor, PatientId = Patient, Status = "Confirmed" });
            var reservation = await db.InsertAsync("scheduling.Reservation", new { SessionQuotaId = Quota, AppUserId = Actor, BookingAttemptId = attempt, Status = "Confirmed", ExpiresAtUtc = Now.AddMinutes(5), ReservedAtUtc = Now });
            return await db.InsertAsync("scheduling.Appointment", new { ReservationId = reservation, SessionQuotaId = Quota, PatientId = Patient, Status = status, ConfirmedAtUtc = Now });
        }
        public async Task<Guid> EncounterAsync(DateTime? at = null, string status = "WaitingCLS") => await db.InsertAsync("clinical.Encounter", new
            { PatientId = Patient, AppointmentId = Guid.NewGuid(), OriginalDoctorId = Doctor, OriginalRoomId = Room, CheckedInAtUtc = at ?? Now, Status = status });
        public Task<Guid> TicketAsync(Guid encounter, Guid targetRoom, string type, string status, int seq, DateTime issued, Guid? item = null) =>
            db.InsertAsync("scheduling.QueueTicket", new { EncounterId = encounter, TargetRoomId = targetRoom, TicketType = type, QueueStatus = status,
                TicketNumber = "T-" + seq, SequenceNumber = seq, IssuedAtUtc = issued, ServiceOrderId = type == "InitialConsultation" ? (Guid?)null : Guid.NewGuid(),
                ServiceOrderItemId = type == "CLS" ? item ?? Guid.NewGuid() : (Guid?)null, CalledAtUtc = status == "Called" ? (DateTime?)issued : null,
                CompletedAtUtc = status == "Finished" ? (DateTime?)issued : null });
        public async Task ChargeAsync(Guid encounter, Guid item, BillingItemStatus status, decimal total, decimal paid)
        {
            await using var scope = app.Services.CreateAsyncScope(); var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            if (!await billing.Set<PriceList>().AnyAsync(x => x.Id == PriceList, Ct))
            {
                billing.Add(new PriceList { Id = PriceList, PriceListCode = PriceList.ToString(), PriceListName = "Journey test", Currency = "VND", EffectiveFromUtc = Now.AddDays(-1), IsActive = true });
                billing.Add(new PriceListItem { Id = PriceItem, PriceListId = PriceList, ItemType = ItemType.CLSService, MedicalServiceId = Guid.NewGuid(), IsActive = true });
            }
            var charge = BillingItem.CreatePending(PriceItem, ItemType.CLSService, "Test CLS", total, 1, Now, encounterId: encounter,
                serviceOrderItemId: item, serviceOrderId: Guid.NewGuid(), patientId: Patient); charge.Status = status; charge.PaidAmount = paid;
            billing.Add(charge); await billing.SaveChangesAsync(Ct);
        }
        public async Task NurseDutyAsync()
        {
            await db.InsertAsync("identity.Staff", new { Id = NurseStaff, AppUserId = Actor, StaffType = "Nurse", IsActive = true });
            await db.InsertAsync("identity.StaffShiftAssignment", new { StaffId = NurseStaff, RoomId = Room, ShiftDate = new DateTime(2087, 2, 4), ShiftName = "Morning", StartsAtUtc = Now.AddHours(-1), EndsAtUtc = Now.AddHours(1) });
        }
        private sealed class FixedClock : IDbClock { public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(new DateTime(2087, 2, 3, 17, 30, 0, DateTimeKind.Utc)); }
        public async ValueTask DisposeAsync() { await app.StopAsync(Ct); await app.DisposeAsync(); await db.DisposeAsync(); }
    }
}
