using System.Net;
using System.Reflection;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.Queues;
using Xunit;

namespace Scheduling.Tests;

public sealed class QueueCommandAuthorizationTests
{
    [Theory]
    [InlineData(Roles.Nurse, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Doctor, HttpStatusCode.OK)]
    [InlineData(Roles.RoomAccount, HttpStatusCode.OK)]
    public async Task Queue_writes_are_for_doctor_and_room_account_only(string role, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var store = DispatchProxy.Create<IQueueStore, QueuePorts>();
        var state = (QueuePorts)(object)store;
        var identity = DispatchProxy.Create<IIdentityPatientsModule, QueuePorts>();
        ((QueuePorts)(object)identity).Room = state.Room;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton(store).AddSingleton(identity);
        await using var app = builder.Build(); app.MapSchedulingEndpoints(); await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, extra:
            [new Claim(AppClaims.RoomId, state.Room.ToString()), new Claim(AppClaims.DoctorId, Guid.NewGuid().ToString())]);
        foreach (var route in new[] { $"rooms/{state.Room}/call-next", $"tickets/{state.Ticket}/start", $"tickets/{state.Ticket}/finish", $"tickets/{state.Ticket}/requeue" })
        {
            using var response = await client.PostAsync("/api/scheduling/staff/" + route, null, ct);
            Assert.Equal(expected, response.StatusCode);
        }
        if (role == Roles.Nurse) Assert.Equal(0, state.Calls);
        else Assert.True(state.Calls > 0);
    }

    public class QueuePorts : DispatchProxy
    {
        public Guid Room { get; set; } = Guid.NewGuid();
        public Guid Ticket { get; } = Guid.NewGuid();
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            var ticket = new CalledTicket(Ticket, "001", 1, "InitialConsultation", "InProgress", Room, null, Guid.NewGuid(), null);
            return method!.Name switch
            {
                nameof(IIdentityPatientsModule.GetRoomAsync) => Task.FromResult<RoomDto?>(new(Room, "P1", "Test room", "Clinical", true)),
                nameof(IIdentityPatientsModule.GetRoomsAsync) => Task.FromResult<IReadOnlyList<RoomDto>>([new(Room, "P1", "Test room", "Clinical", true)]),
                nameof(IQueueStore.GetDoctorRoomTodayAsync) => Task.FromResult<Guid?>(Room),
                nameof(IQueueStore.ResolveStationAsync) => Task.FromResult(new StationLookup(Guid.NewGuid())),
                nameof(IQueueStore.GetAllocatedTicketAsync) => Task.FromResult<CalledTicket?>(ticket),
                nameof(IQueueStore.TransitionAsync) => Task.FromResult(new QueueOutcome(QueueResult.Called, ticket)),
                _ => throw new NotSupportedException(method.Name)
            };
        }
    }
}
