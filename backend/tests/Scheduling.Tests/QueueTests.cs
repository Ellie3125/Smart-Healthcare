using System.Net;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.Queues;
using Scheduling.Domain.Enums;
using Xunit;

namespace Scheduling.Tests;

public sealed class QueueTests
{
    [Theory]
    [InlineData("InitialConsultation", "P1-001")]
    [InlineData("CLS", "P1-001")]
    [InlineData("ReturnConsultation", "P1-R001")]
    public async Task Queue_commands_return_display_numbers_for_each_ticket_kind(string kind, string expected)
    {
        var room = Guid.NewGuid();
        var store = new Store(room) { Kind = kind };
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IQueueStore>(store);
        var directory = System.Reflection.DispatchProxy.Create<IIdentityPatientsModule, RoomQueueEndpointTests.NurseDirectory>();
        ((RoomQueueEndpointTests.NurseDirectory)(object)directory).RoomId = room;
        builder.Services.AddSingleton(directory);
        await using var app = builder.Build(); app.MapSchedulingEndpoints(); await app.StartAsync(Ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount, extra: new Claim(AppClaims.RoomId, room.ToString()));
        foreach (var route in new[] { $"rooms/{room}/call-next", $"tickets/{store.OwnTicket}/start", $"tickets/{store.OwnTicket}/finish", $"tickets/{store.OwnTicket}/requeue" })
        {
            using var response = await client.PostAsync("/api/scheduling/staff/" + route, null, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal(expected, json.RootElement.GetProperty("displayNumber").GetString());
            Assert.Equal("001", json.RootElement.GetProperty("ticketNumber").GetString());
        }
    }

    private static readonly DateTime Now = new(2026, 11, 18, 3, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WaitingTicket Ticket(TicketType type, int sequence, int minutesAgo) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, sequence, Now.AddMinutes(-minutesAgo));

    [Theory]
    [InlineData(0, TicketType.InitialConsultation)]
    [InlineData(1, TicketType.InitialConsultation)]
    [InlineData(2, TicketType.ReturnConsultation)]
    public void Fairness_mixes_two_new_then_one_return(int newStreak, TicketType expectedFirst)
    {
        var waiting = new[] { Ticket(TicketType.ReturnConsultation, 1, 5), Ticket(TicketType.InitialConsultation, 7, 3),
            Ticket(TicketType.InitialConsultation, 6, 2) };

        var order = QueueFairness.Order(waiting, newStreak, Now, QueuePolicy.Default).ToList();

        Assert.Equal(expectedFirst, order[0].TicketType);
        // Trong nhóm mới giữ thứ tự số cấp lúc check in, không theo giờ phát.
        Assert.Equal(new[] { 6, 7 }, order.Where(x => x.TicketType != TicketType.ReturnConsultation).Select(x => x.SequenceNumber));
    }

    [Fact]
    public void Fairness_serves_a_group_waiting_past_the_threshold_first()
    {
        var starvedReturn = new[] { Ticket(TicketType.ReturnConsultation, 1, 25), Ticket(TicketType.InitialConsultation, 1, 10) };
        var starvedNew = new[] { Ticket(TicketType.ReturnConsultation, 1, 5), Ticket(TicketType.InitialConsultation, 1, 30) };

        Assert.Equal(TicketType.ReturnConsultation, QueueFairness.Order(starvedReturn, 0, Now, QueuePolicy.Default).First().TicketType);
        Assert.Equal(TicketType.InitialConsultation, QueueFairness.Order(starvedNew, 2, Now, QueuePolicy.Default).First().TicketType);
    }

    [Fact]
    public void Fairness_serves_the_other_group_when_one_is_empty()
    {
        var onlyReturn = new[] { Ticket(TicketType.ReturnConsultation, 2, 1), Ticket(TicketType.ReturnConsultation, 1, 2) };

        Assert.Equal(new[] { 1, 2 }, QueueFairness.Order(onlyReturn, 0, Now, QueuePolicy.Default).Select(x => x.SequenceNumber));
    }

    // W3-08: tài khoản phòng chỉ thao tác vé/phòng của RoomId trong token; vai trò không gọi số bị 403.
    [Fact]
    public async Task Room_account_acts_only_on_its_own_room()
    {
        var roomA = Guid.NewGuid();
        var store = new Store(roomA);
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IQueueStore>(store);
        await using var app = builder.Build();
        app.MapSchedulingEndpoints();
        await app.StartAsync(Ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var otherRoom = Guid.NewGuid();

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/scheduling/staff/rooms/{roomA}/call-next", null, Ct)).StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount, extra: new Claim(AppClaims.RoomId, roomA.ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/scheduling/staff/rooms/{otherRoom}/call-next", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/scheduling/staff/tickets/{store.OtherRoomTicket}/start", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/scheduling/staff/tickets/{store.OwnTicket}/start", null, Ct)).StatusCode);
        Assert.Equal(roomA, store.LastRequiredRoom);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/scheduling/staff/tickets/{store.OwnTicket}/start", null, Ct)).StatusCode);

        // Bác sĩ ngoài phòng của phiên hôm nay cũng bị chặn.
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Doctor, extra: new Claim(AppClaims.DoctorId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/scheduling/staff/rooms/{otherRoom}/call-next", null, Ct)).StatusCode);
    }

    private sealed class Store(Guid room) : IQueueStore
    {
        public string Kind { get; init; } = "InitialConsultation";
        private CalledTicket Ticket(Guid id) => new(id, "001", 1, Kind, "InProgress", room, null, Guid.NewGuid(), null);
        public Guid OwnTicket { get; } = Guid.NewGuid();
        public Guid OtherRoomTicket { get; } = Guid.NewGuid();
        public Guid? LastRequiredRoom { get; private set; }

        public Task<QueueOutcome> TransitionAsync(TicketCommand command, TicketTransition transition, CancellationToken ct)
        {
            LastRequiredRoom = command.RequiredRoomId;
            var ticketRoom = command.TicketId == OwnTicket ? room : Guid.NewGuid();
            return Task.FromResult(command.RequiredRoomId is { } required && required != ticketRoom
                ? new QueueOutcome(QueueResult.WrongRoom)
                : new QueueOutcome(QueueResult.Called, Ticket(command.TicketId) with { TargetRoomId = ticketRoom }));
        }

        public Task<Guid?> GetDoctorRoomTodayAsync(Guid doctorId, CancellationToken ct) => Task.FromResult<Guid?>(room);
        public Task<StationLookup> ResolveStationAsync(Guid roomId, Guid? stationId, string roomName, CancellationToken ct) => Task.FromResult(new StationLookup(Guid.NewGuid()));
        public Task<CalledTicket?> GetAllocatedTicketAsync(Guid stationId, CancellationToken ct) => Task.FromResult<CalledTicket?>(Ticket(OwnTicket));
        public Task<IReadOnlyList<WaitingTicket>> ListWaitingAsync(Guid roomId, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountRecentNewCallsAsync(Guid roomId, DateTime nowUtc, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<QueueOutcome> TryAllocateAsync(Allocate command, CancellationToken ct) => throw new NotSupportedException();
    }
}
