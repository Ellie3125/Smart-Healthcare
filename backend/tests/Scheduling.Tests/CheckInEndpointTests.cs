using System.Net;
using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;
using Xunit;

namespace Scheduling.Tests;

public sealed class CheckInEndpointTests
{
    [Fact]
    public async Task Reception_routes_require_staff_receptionist_and_use_claim_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        await using var app = builder.Build();
        app.MapSchedulingCheckInEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var appointment = Guid.NewGuid();
        var route = $"/api/scheduling/staff/appointments/{appointment}/check-in";
        const string today = "/api/scheduling/staff/appointments/today";
        using var anonymous = await client.PostAsync(route, null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Staff),
            (Roles.Doctor, Channels.Staff), (Roles.Receptionist, Channels.Patient) })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, channel: channel);
            using var post = await client.PostAsync(route, null, ct);
            using var get = await client.GetAsync(today, ct);
            Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        }
        var actor = Guid.NewGuid();
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist, actor);
        using var ok = await client.PostAsync(route, null, ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var store = (CheckInTestServices.Store)app.Services.GetRequiredService<ICheckInStore>();
        Assert.Equal(new CheckInAppointment(appointment, actor, null), store.LastCommand);
        foreach (var query in new[] { "?page=0", "?pageSize=51", "?page=invalid", "?doctorId=invalid", "?appointmentNumber=" + new string('a', 65) })
        {
            using var bad = await client.GetAsync(today + query, ct);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        foreach (var (error, expected) in new[] { (SchedulingErrorCode.AppointmentNotFound, HttpStatusCode.NotFound),
            (SchedulingErrorCode.CheckInDeadlinePassed, HttpStatusCode.Conflict), (SchedulingErrorCode.InvalidInput, HttpStatusCode.BadRequest) })
        {
            store.Error = error;
            using var response = await client.PostAsync(route, null, ct);
            Assert.Equal(expected, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(error.ToString(), json.RootElement.GetProperty("code").GetString());
        }
    }
}
