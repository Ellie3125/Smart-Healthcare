using System.Net;
using System.Text.Json;
using Billing.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Api;
using Scheduling.Application.DoctorSearch;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorSearchEndpointTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAppearsInOpenApiAndRequiresPatientJwt(bool hasBatchBilling)
    {
        var ct = TestContext.Current.CancellationToken;
        var date = new DateOnly(2026, 11, 15);
        var doctorId = Guid.NewGuid();
        var rankId = Guid.NewGuid();
        var specialtyId = Guid.NewGuid();
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        const string doctorConfig = "Scheduling:DoctorSearch:DevelopmentDoctors:0:";
        builder.Configuration[doctorConfig + "DoctorId"] = doctorId.ToString("D");
        builder.Configuration[doctorConfig + "FullName"] = "BS giả lập";
        builder.Configuration[doctorConfig + "DoctorRankId"] = rankId.ToString("D");
        builder.Configuration[doctorConfig + "SpecialtyId"] = specialtyId.ToString("D");
        builder.Services.AddSingleton<IDoctorSearchQuotaReader>(
            new DoctorSearchTests.Quotas([(date, new DoctorSearchQuota(doctorId, 30, 10, 14, 1))]));
        if (hasBatchBilling)
        {
            builder.Services.AddSingleton<IBillingModule, DoctorSearchTests.BatchBilling>();
        }

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
        using var invalid = await client.GetAsync(url + "&pageSize=51", ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var response = await client.GetAsync(url, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (hasBatchBilling)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
            Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(doctorId, item.GetProperty("doctorId").GetGuid());
            Assert.Equal(5, item.GetProperty("availableSlots").GetInt32());
            Assert.Equal(150000m, item.GetProperty("examFee").GetDecimal());
        }
        else
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("ExamFeeBatchUnavailable", json.RootElement.GetProperty("code").GetString());
        }
    }
}
