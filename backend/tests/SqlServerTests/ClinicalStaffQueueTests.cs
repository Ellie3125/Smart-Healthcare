using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure;
using Scheduling.Application.RoomQueues;
using Microsoft.Extensions.DependencyInjection;
using Clinical.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application;
using Scheduling.Api;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Billing.Infrastructure;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalStaffQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Nurse_HTTP_composes_real_Scheduling_Clinical_and_Identity_readers()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync(services => services
            .AddSchedulingInfrastructure(TestDatabase.RequiredConnectionString()).AddSchedulingApplication().AddSchedulingApi()
            .AddBillingInfrastructure(TestDatabase.RequiredConnectionString()));
        await using var scope = f.App.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
        var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var encounter = await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Clinical.Domain.Entities.Encounter>()
            .SingleAsync(e => e.Id == f.EncounterId, Ct);
        var now = DateTime.UtcNow;
        var staffId = Guid.NewGuid();
        identity.Add(new AppUser { Id = f.UserId, UserName = "nurse-" + f.UserId, PasswordHash = "test-only", IsActive = true, CreatedAtUtc = now });
        identity.Add(new Staff { Id = staffId, AppUserId = f.UserId, StaffCode = staffId.ToString(), FullName = "Test nurse",
            StaffType = StaffType.Nurse, IsActive = true, CreatedAtUtc = now });
        identity.Add(new Room { Id = encounter.OriginalRoomId, RoomNumber = encounter.OriginalRoomId.ToString(), RoomName = "Test room",
            RoomType = RoomType.Clinical, DepartmentName = "Test", IsActive = true });
        identity.Add(new StaffShiftAssignment { Id = Guid.NewGuid(), StaffId = staffId, RoomId = encounter.OriginalRoomId,
            ShiftDate = DateOnly.FromDateTime(now.AddHours(7)), ShiftName = ShiftName.FullDay, StartsAtUtc = now.AddHours(-1), EndsAtUtc = now.AddHours(1) });
        var ticketId = Guid.NewGuid();
        scheduling.Add(new QueueTicket { Id = ticketId, EncounterId = encounter.Id, TargetRoomId = encounter.OriginalRoomId,
            TicketNumber = ticketId.ToString(), TicketType = TicketType.InitialConsultation, QueueStatus = QueueStatus.Waiting,
            SequenceNumber = 1, IssuedAtUtc = now });
        try
        {
            await identity.SaveChangesAsync(Ct);
            await scheduling.SaveChangesAsync(Ct);
            using var client = f.Client(Roles.Nurse);
            using var response = await client.GetAsync($"/api/clinical/staff/nurse/queue?roomId={encounter.OriginalRoomId}", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var row = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
            Assert.Equal(f.EncounterId, row.GetProperty("encounterId").GetGuid());
            Assert.Equal(ticketId, row.GetProperty("queueTicketId").GetGuid());
            Assert.Equal("Waiting", row.GetProperty("status").GetString());
            Assert.Equal(encounter.OriginalRoomId + "-001", row.GetProperty("displayNumber").GetString());
        }
        finally
        {
            await scheduling.Set<QueueTicket>().Where(t => t.Id == ticketId).ExecuteDeleteAsync(Ct);
            await identity.StaffShiftAssignments.Where(s => s.StaffId == staffId).ExecuteDeleteAsync(Ct);
            await identity.Staffs.Where(s => s.Id == staffId).ExecuteDeleteAsync(Ct);
            await identity.Rooms.Where(r => r.Id == encounter.OriginalRoomId).ExecuteDeleteAsync(Ct);
            await identity.AppUsers.Where(u => u.Id == f.UserId).ExecuteDeleteAsync(Ct);
        }
    }

    [Fact]
    public async Task Nurse_without_duty_is_denied_and_invalid_rooms_and_pages_are_rejected()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var client = f.Client(Roles.Nurse);
        var room = Guid.NewGuid();
        using var denied = await client.GetAsync($"/api/clinical/staff/nurse/queue?roomId={room}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var query in new[] { $"roomId={Guid.Empty}", $"roomId={room}&pageSize=0", $"roomId={room}&page=1001" })
        {
            using var invalid = await client.GetAsync("/api/clinical/staff/nurse/queue?" + query, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Fact]
    public async Task Nurse_duty_requires_active_staff_account_room_and_current_shift_in_local_date()
    {
        await using var f = await TestDatabase.OpenAsync();
        var now = new DateTime(2026, 11, 18, 2, 0, 0, DateTimeKind.Utc);
        var user = await f.InsertAsync("identity.AppUser", new { IsActive = true });
        var staff = await f.InsertAsync("identity.Staff", new { AppUserId = user, StaffType = "Nurse", IsActive = true });
        var room = await f.InsertAsync("identity.Room", new { RoomType = "Consultation", IsActive = true });
        var shift = await f.InsertAsync("identity.StaffShiftAssignment", new { StaffId = staff, RoomId = room,
            ShiftDate = now.Date, ShiftName = "Morning", StartsAtUtc = now, EndsAtUtc = now.AddHours(1) });
        await using var db = f.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule identity = new IdentityPatientsModule(db);
        Assert.True(await identity.IsNurseOnDutyAsync(user, room, now, Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, Guid.NewGuid(), now, Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, room, now.AddTicks(-1), Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, room, now.AddHours(1), Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, room, now.AddDays(1), Ct));
        foreach (var (table, id, column, bad, good) in new[] {
            ("[identity].[Staff]", staff, "IsActive", "0", "1"), ("[identity].[AppUser]", user, "IsActive", "0", "1"),
            ("[identity].[Room]", room, "IsActive", "0", "1"), ("[identity].[Staff]", staff, "StaffType", "N'Doctor'", "N'Nurse'") })
        {
            await f.QueryAsync($"UPDATE {table} SET {column}={bad} WHERE Id='{id}'");
            Assert.False(await identity.IsNurseOnDutyAsync(user, room, now, Ct));
            await f.QueryAsync($"UPDATE {table} SET {column}={good} WHERE Id='{id}'");
        }
        await f.QueryAsync($"UPDATE [identity].[StaffShiftAssignment] SET ShiftDate='2026-11-17' WHERE Id='{shift}'");
        Assert.False(await identity.IsNurseOnDutyAsync(user, room, now, Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(Guid.Empty, room, now, Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, Guid.Empty, now, Ct));
        Assert.False(await identity.IsNurseOnDutyAsync(user, room, DateTime.SpecifyKind(now, DateTimeKind.Unspecified), Ct));
    }

    [Fact]
    public async Task Queue_SQL_limits_room_local_day_type_status_and_orders_before_paging()
    {
        await using var f = await TestDatabase.OpenAsync();
        var room = Guid.NewGuid();
        var start = new DateTime(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc);
        async Task<Guid> Ticket(Guid target, DateTime at, int sequence, string type = "InitialConsultation", string status = "Waiting") =>
            await f.InsertAsync("scheduling.QueueTicket", new { TargetRoomId = target, IssuedAtUtc = at, SequenceNumber = sequence,
                TicketType = type, QueueStatus = status });
        var second = await Ticket(room, start, 2);
        var first = await Ticket(room, start, 1);
        var last = await Ticket(room, start.AddDays(1).AddSeconds(-1), 3);
        await Ticket(Guid.NewGuid(), start, 1);
        await Ticket(room, start.AddSeconds(-1), 1);
        await Ticket(room, start.AddDays(1), 1);
        await Ticket(room, start, 1, "ReturnConsultation");
        await Ticket(room, start, 1, status: "Finished");
        await Ticket(room, start, 1, status: "Cancelled");
        await using var db = f.CreateContext<SchedulingDbContext>(o => new(o));
        await using var services = new ServiceCollection().AddSchedulingInfrastructure(TestDatabase.RequiredConnectionString())
            .AddScoped(_ => db).BuildServiceProvider();
        var reader = services.GetRequiredService<IClinicalQueueReader>();
        var page = await reader.ReadAsync(room, start.AddHours(8), 1, 2, Ct);
        Assert.Equal(new[] { first, second }, page.Items.Select(t => t.QueueTicketId));
        Assert.True(page.HasMore);
        var next = await reader.ReadAsync(room, start.AddHours(8), 2, 2, Ct);
        Assert.Equal(last, Assert.Single(next.Items).QueueTicketId);
        Assert.False(next.HasMore);
    }

    [Fact]
    public async Task Nurse_context_reads_latest_vitals_only_for_encounters_in_the_requested_room()
    {
        await using var f = await TestDatabase.OpenAsync();
        var room = Guid.NewGuid();
        var encounter = await f.InsertAsync("clinical.Encounter", new { OriginalRoomId = room, Status = "InProgress" });
        var other = await f.InsertAsync("clinical.Encounter", new { OriginalRoomId = Guid.NewGuid(), Status = "InProgress" });
        var at = new DateTime(2026, 11, 18, 2, 0, 0, DateTimeKind.Utc);
        await f.InsertAsync("clinical.VitalSign", new { EncounterId = encounter, RecordedAtUtc = at.AddMinutes(-1) });
        await f.InsertAsync("clinical.VitalSign", new { EncounterId = encounter, RecordedAtUtc = at });
        await using var db = f.CreateContext<ClinicalDbContext>(o => new(o));
        var reader = new EfClinicalStaffListReader(db);
        var row = Assert.Single(await reader.NurseContextsAsync([encounter, other], room, Ct));
        Assert.Equal(encounter, row.EncounterId);
        Assert.Equal(at, row.LastVitalsAtUtc);
        Assert.NotEmpty(Convert.FromBase64String(row.Version));
    }

    [Fact]
    public async Task Records_list_only_the_calling_doctors_encounters_and_validate_query()
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var client = f.Client();
        using var response = await client.GetAsync("/api/clinical/staff/doctor/records", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(f.EncounterId, body.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var other = f.Client(doctorId: Guid.NewGuid());
        using var hidden = await other.GetAsync("/api/clinical/staff/doctor/records", Ct);
        Assert.Empty((await hidden.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        using var missingDoctor = f.Client(doctorId: Guid.Empty);
        using var forbidden = await missingDoctor.GetAsync("/api/clinical/staff/doctor/records", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?keyword=not-an-encounter-id" })
        {
            using var invalid = await client.GetAsync("/api/clinical/staff/doctor/records" + query, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/clinical/staff/doctor/records", Roles.Doctor)]
    [InlineData("/api/clinical/staff/nurse/queue?roomId=11111111-1111-1111-1111-111111111111", Roles.Nurse)]
    public async Task Reads_require_the_correct_role_and_staff_channel(string route, string role)
    {
        await using var f = await ClinicalRecordFixture.CreateAsync();
        using var anonymous = f.Client(authenticate: false);
        using var unauthenticated = await anonymous.GetAsync(route, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        foreach (var (wrongRole, channel) in new[] { (role, Channels.Patient), (Roles.Admin, Channels.Staff),
                     (Roles.Patient, Channels.Patient), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff),
                     (role == Roles.Nurse ? Roles.Doctor : Roles.Nurse, Channels.Staff) })
        {
            using var wrong = f.Client(wrongRole, channel);
            using var denied = await wrong.GetAsync(route, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }
}
