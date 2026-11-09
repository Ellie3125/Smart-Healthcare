using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
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
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = signingKey;
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Scheduling.Tests";
        builder.Configuration[$"Scheduling:DoctorDays:DevelopmentDoctorRooms:{doctorId:D}"] = Guid.NewGuid().ToString("D");
        builder.Services.AddIdentityPatientsAuth(builder.Configuration);
        builder.Services.RemoveAll<IAuthService>();
        builder.Services.AddOpenApi();
        builder.Services.AddSchedulingApplication().AddSchedulingApi();
        builder.Services.AddSingleton<IDoctorDayStore, Store>();
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

        var url = $"{route}/{doctorId:D}/2026-11-10";
        using var anonymous = await client.PostAsync(url, null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        Authorize(Roles.Patient);
        using var patientOpen = await client.PostAsync(url, null, ct);
        using var patientClose = await client.PostAsync(url + "/close", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, patientOpen.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patientClose.StatusCode);

        Authorize(Roles.Admin);
        using var opened = await client.PostAsync(url, null, ct);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        using var quota = JsonDocument.Parse(await opened.Content.ReadAsStringAsync(ct));
        Assert.Equal("2026-11-10T04:00:00Z", quota.RootElement.GetProperty("checkInDeadlineUtc").GetString());

        app.Configuration["Scheduling:DoctorDays:CheckInDeadline"] = "12:15";
        using var nextDay = await client.PostAsync($"{route}/{doctorId:D}/2026-11-11", null, ct);
        Assert.Equal(HttpStatusCode.Created, nextDay.StatusCode);
        using var nextQuota = JsonDocument.Parse(await nextDay.Content.ReadAsStringAsync(ct));
        Assert.Equal("2026-11-11T05:15:00Z", nextQuota.RootElement.GetProperty("checkInDeadlineUtc").GetString());

        using var blocked = await client.PostAsync(url + "/close", null, ct);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync(ct));
        Assert.Equal("HasBookings", problem.RootElement.GetProperty("code").GetString());

        void Authorize(string role)
        {
            var token = new JwtSecurityToken("Scheduling.Tests", "Scheduling.Tests", [new Claim("role", role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
    }

    private sealed class Store : IDoctorDayStore
    {
        public Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct) => Task.FromResult<DoctorDayQuota?>(quota);
        public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
            Task.FromResult(DoctorDayResult.HasBookings);
    }
}
