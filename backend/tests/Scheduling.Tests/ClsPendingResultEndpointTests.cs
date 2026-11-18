using System.Net;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.Queues;
using Xunit;

namespace Scheduling.Tests;

public sealed class ClsPendingResultEndpointTests
{
    [Fact]
    public async Task Only_the_room_account_can_list_its_own_pending_results()
    {
        var ct = TestContext.Current.CancellationToken; var room = Guid.NewGuid();
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IClsPendingResultReader, Reader>();
        var directory = System.Reflection.DispatchProxy.Create<IIdentityPatientsModule, RoomQueueEndpointTests.NurseDirectory>();
        ((RoomQueueEndpointTests.NurseDirectory)(object)directory).RoomId = room;
        builder.Services.AddSingleton(directory);
        await using var app = builder.Build(); app.MapSchedulingEndpoints(); await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var path = $"/api/scheduling/staff/rooms/{room}/pending-results";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path, ct)).StatusCode);
        foreach (var role in new[] { Roles.Patient, Roles.Doctor, Roles.Admin, Roles.Nurse, Roles.Receptionist })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path, ct)).StatusCode);
        }
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount, channel: Channels.Patient,
            extra: new Claim(AppClaims.RoomId, room.ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path, ct)).StatusCode);
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount, extra: new Claim(AppClaims.RoomId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path, ct)).StatusCode);
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.RoomAccount, extra: new Claim(AppClaims.RoomId, room.ToString()));
        using var success = await client.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await success.Content.ReadAsStringAsync(ct));
        Assert.Equal("P1-007", json.RootElement.GetProperty("items")[0].GetProperty("displayNumber").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=0", ct)).StatusCode);
    }
    private sealed class Reader : IClsPendingResultReader
    {
        public Task<ClsPendingResultPage> ListAsync(Guid roomId, int page, int pageSize, CancellationToken ct) => Task.FromResult(new ClsPendingResultPage([new(Guid.NewGuid(), "original-ticket", Guid.NewGuid(), Guid.NewGuid(), null, 7)], false));
    }
}
