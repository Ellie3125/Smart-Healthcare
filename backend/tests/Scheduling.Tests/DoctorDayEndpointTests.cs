using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Scheduling.Api;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorDayEndpointTests
{
    [Fact]
    public async Task OpenApiAndHttpExposeStaffAdminDoctorDays()
    {
        var ct = TestContext.Current.CancellationToken;
        var doctorId = Guid.NewGuid();
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IDoctorDirectory, ActiveDirectory>();
        await using var app = builder.Build();
        app.MapSchedulingEndpoints();
        app.MapOpenApi();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        const string route = "/api/scheduling/admin/doctor-days";
        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", ct));
        var paths = openApi.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty(route + "/{doctorId}/{serviceDate}").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty(route + "/{doctorId}/{serviceDate}/close").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty(route).TryGetProperty("get", out _));
        var room = JsonContent.Create(new { roomId = Guid.NewGuid() });

        var url = $"{route}/{doctorId:D}/2026-11-10";
        using var anonymous = await client.PostAsync(url, room, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient);
        using var patientOpen = await client.PostAsync(url, room, ct);
        using var patientList = await client.GetAsync(route + "?serviceDate=2026-11-10", ct);
        Assert.Equal(HttpStatusCode.Forbidden, patientList.StatusCode);
        using var patientClose = await client.PostAsync(url + "/close", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, patientOpen.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patientClose.StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Admin);
        using var noRoom = await client.PostAsync(url, JsonContent.Create(new { }), ct);
        Assert.Equal(HttpStatusCode.BadRequest, noRoom.StatusCode);
        using var list = await client.GetAsync(route + "?serviceDate=2026-11-10", ct);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("[]", await list.Content.ReadAsStringAsync(ct));
        using var badDate = await client.GetAsync(route + "?serviceDate=10-11-2026", ct);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
        using var opened = await client.PostAsync(url, room, ct);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        using var quota = JsonDocument.Parse(await opened.Content.ReadAsStringAsync(ct));
        Assert.Equal("2026-11-10T04:00:00Z", quota.RootElement.GetProperty("checkInDeadlineUtc").GetString());

        app.Configuration["Scheduling:DoctorDays:CheckInDeadline"] = "12:15";
        using var nextDay = await client.PostAsync($"{route}/{doctorId:D}/2026-11-11", room, ct);
        Assert.Equal(HttpStatusCode.Created, nextDay.StatusCode);
        using var nextQuota = JsonDocument.Parse(await nextDay.Content.ReadAsStringAsync(ct));
        Assert.Equal("2026-11-11T05:15:00Z", nextQuota.RootElement.GetProperty("checkInDeadlineUtc").GetString());

        using var blocked = await client.PostAsync(url + "/close", null, ct);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync(ct));
        Assert.Equal("HasBookings", problem.RootElement.GetProperty("code").GetString());

    }

    internal sealed class ActiveDirectory : IDoctorDirectory
    {
        public Task<bool> IsActiveDoctorAsync(Guid doctorId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsActiveClinicalRoomAsync(Guid roomId, CancellationToken ct) => Task.FromResult(true);
    }

    internal sealed class Store : IDoctorDayStore
    {
        public Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct) => Task.FromResult<DoctorDayQuota?>(quota);
        public Task<IReadOnlyList<DoctorDaySummary>> ListAsync(DateOnly serviceDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DoctorDaySummary>>([]);
        public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
            Task.FromResult(DoctorDayResult.HasBookings);
    }
}
