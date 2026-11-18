using System.Net;
using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.DoctorSearch;
using Scheduling.Contracts;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorSearchEndpointTests
{
    [Fact]
    public async Task SearchAppearsInOpenApiAndRequiresPatientJwt()
    {
        var ct = TestContext.Current.CancellationToken;
        var date = new DateOnly(2026, 11, 15);
        var doctorId = Guid.NewGuid();
        var rankId = Guid.NewGuid();
        var specialtyId = Guid.NewGuid();
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IDoctorSearchDirectory>(
            new DoctorSearchTests.Directory(new DoctorSearchProfile(doctorId, "BS giả lập", rankId, specialtyId)));
        builder.Services.AddSingleton<IDoctorSearchQuotaReader>(
            new DoctorSearchTests.Quotas([(date, new DoctorSearchQuota(doctorId, 30, 10, 14, 1))]));

        await using var app = builder.Build();
        app.MapSchedulingEndpoints();
        app.MapOpenApi();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        const string route = "/api/scheduling/patient/doctors";
        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", ct));
        Assert.True(openApi.RootElement.GetProperty("paths").GetProperty(route).TryGetProperty("get", out _));

        var url = $"{route}?serviceDate=2026-11-15&doctorRankId={rankId:D}&specialtyId={specialtyId:D}";
        using var anonymous = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist);
        using var staff = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient);
        foreach (var invalidUrl in new[] { route, route + "?serviceDate=invalid",
            route + "?serviceDate=2026-11-15&doctorRankId=invalid", url + "&page=invalid", url + "&pageSize=51" })
        {
            using var invalid = await client.GetAsync(invalidUrl, ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(ct));
            Assert.Equal("InvalidInput", problem.RootElement.GetProperty("code").GetString());
        }

        using var response = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.False(json.RootElement.GetProperty("hasMore").GetBoolean());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(doctorId, item.GetProperty("doctorId").GetGuid());
        Assert.Equal(5, item.GetProperty("availableSlots").GetInt32());
        Assert.Equal(150000m, item.GetProperty("examFee").GetDecimal());
        // Tiếp tân dùng route staff, cùng DTO; bệnh nhân không gọi được route staff.
        var staffUrl = url.Replace(route, "/api/scheduling/staff/doctors");
        using var patientOnStaff = await client.GetAsync(staffUrl, ct);
        Assert.Equal(HttpStatusCode.Forbidden, patientOnStaff.StatusCode);
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist);
        using var receptionist = JsonDocument.Parse(await client.GetStringAsync(staffUrl, ct));
        Assert.Equal(doctorId, Assert.Single(receptionist.RootElement.GetProperty("items").EnumerateArray()).GetProperty("doctorId").GetGuid());
        using var badDate = await client.GetAsync("/api/scheduling/staff/doctors?serviceDate=x", ct);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }
}
