using System.Net;
using Scheduling.Api;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.RoomQueues;
using System.Text.Json;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using IdentityPatients.Contracts;
using System.Reflection;
using Shared.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Scheduling.Tests;

public sealed class RoomQueueEndpointTests
{
    [Fact]
    public async Task Public_display_is_available_without_authentication()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IRoomQueueReader, Reader>();
        await using var app = builder.Build();
        app.MapSchedulingRoomQueueEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.GetAsync($"/api/scheduling/public/display?roomId={Guid.NewGuid()}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(new[] { "tickets" }, json.RootElement.EnumerateObject().Select(x => x.Name));
        var ticket = Assert.Single(json.RootElement.GetProperty("tickets").EnumerateArray());
        Assert.Equal(new[] { "sequenceNumber", "ticketNumber" }, ticket.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal("Q-007", ticket.GetProperty("ticketNumber").GetString());
    }

    [Fact]
    public async Task Staff_queue_enforces_channel_role_room_claim_and_doctor_assignment()
    {
        var ct = TestContext.Current.CancellationToken;
        var reader = new Reader();
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IRoomQueueReader>(reader);
        builder.Services.AddSingleton<IDbClock>(new DoctorSearchTests.FakeDbClock(new DateTime(2026, 11, 16, 17, 30, 0, DateTimeKind.Utc)));
        var identity = DispatchProxy.Create<IIdentityPatientsModule, NurseDirectory>();
        ((NurseDirectory)(object)identity).RoomId = reader.RoomId;
        builder.Services.AddSingleton(identity);
        await using var app = builder.Build();
        app.MapSchedulingRoomQueueEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var route = $"/api/scheduling/staff/rooms/{reader.RoomId}/queue";
        using var anonymous = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Staff),
            (Roles.Nurse, Channels.Patient), (Roles.Receptionist, Channels.Patient) })
        {
            Authorize(client, app.Configuration, role, channel);
            using var forbidden = await client.GetAsync(route, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        foreach (var role in new[] { Roles.Receptionist, Roles.Nurse })
        {
            Authorize(client, app.Configuration, role, Channels.Staff);
            using var ok = await client.GetAsync(route, ct);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        Authorize(client, app.Configuration, Roles.RoomAccount, Channels.Staff, room: reader.RoomId);
        using var ownRoom = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.OK, ownRoom.StatusCode);
        using var otherRoom = await client.GetAsync($"/api/scheduling/staff/rooms/{Guid.NewGuid()}/queue", ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherRoom.StatusCode);
        Authorize(client, app.Configuration, Roles.RoomAccount, Channels.Staff);
        using var noRoomClaim = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noRoomClaim.StatusCode);
        Authorize(client, app.Configuration, Roles.Doctor, Channels.Staff, doctor: reader.DoctorId);
        using var ownDoctor = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.OK, ownDoctor.StatusCode);
        using var unassigned = await client.GetAsync($"/api/scheduling/staff/rooms/{Guid.NewGuid()}/queue", ct);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Authorize(client, app.Configuration, Roles.Doctor, Channels.Staff);
        using var noDoctorClaim = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noDoctorClaim.StatusCode);
        Authorize(client, app.Configuration, Roles.Nurse, Channels.Staff);
        using var nurseOtherRoom = await client.GetAsync($"/api/scheduling/staff/rooms/{Guid.NewGuid()}/queue", ct);
        Assert.Equal(HttpStatusCode.Forbidden, nurseOtherRoom.StatusCode);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=invalid" })
        {
            using var bad = await client.GetAsync(route + query, ct);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        foreach (var query in new[] { "", "?roomId=invalid", "?roomId=" + Guid.Empty })
        {
            using var bad = await client.GetAsync("/api/scheduling/public/display" + query, ct);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
    }

    private static void Authorize(HttpClient client, IConfiguration config, string role, string channel,
        Guid? room = null, Guid? doctor = null)
    {
        var claims = new List<Claim> { new("role", role), new(AppClaims.Channel, channel), new("sub", Guid.NewGuid().ToString()) };
        if (room is { } r) claims.Add(new(AppClaims.RoomId, r.ToString()));
        if (doctor is { } d) claims.Add(new(AppClaims.DoctorId, d.ToString()));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    private sealed class Reader : IRoomQueueReader
    {
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid DoctorId { get; } = Guid.NewGuid();
        public Task<RoomQueuePage?> GetStaffAsync(Guid roomId, Guid? doctorId, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<RoomQueuePage?>(doctorId is not null && (doctorId != DoctorId || roomId != RoomId)
                ? null : new(new(2026, 11, 17), page, pageSize, false, []));
        public Task<PublicRoomDisplay> GetPublicAsync(Guid roomId, CancellationToken ct) => Task.FromResult(new PublicRoomDisplay([new("Q-007", 7)]));
    }

    public class NurseDirectory : DispatchProxy
    {
        public Guid RoomId { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == nameof(IIdentityPatientsModule.IsNurseOnDutyAsync)
            ? Task.FromResult((Guid)args![1]! == RoomId) : throw new NotSupportedException();
    }
}
