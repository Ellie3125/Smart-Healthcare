using System.Net;
using System.Net.Http.Json;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.MedicalServiceAdmin;
using Scheduling.Api;
using Xunit;

namespace Scheduling.Tests;

public sealed class MedicalServiceAdminEndpointTests
{
    [Theory]
    [InlineData("GET", "/admin/medical-services")]
    [InlineData("POST", "/admin/medical-services")]
    [InlineData("PUT", "/admin/medical-services/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/admin/medical-services/11111111-1111-1111-1111-111111111111/rooms/22222222-2222-2222-2222-222222222222")]
    [InlineData("GET", "/admin/service-stations")]
    [InlineData("POST", "/admin/service-stations")]
    [InlineData("PUT", "/admin/service-stations/11111111-1111-1111-1111-111111111111")]
    public async Task Admin_routes_reject_anonymous_other_roles_and_wrong_channel(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IMedicalServiceCatalog, Catalog>();
        await using var app = builder.Build();
        app.MapSchedulingEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        using var anonymous = await client.SendAsync(new(new HttpMethod(method), "/api/scheduling" + path), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Doctor, Channels.Staff),
            (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff), (Roles.Admin, Channels.Patient) })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, channel: channel);
            using var response = await client.SendAsync(new(new HttpMethod(method), "/api/scheduling" + path), ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Admin);
        using var ok = await client.SendAsync(new(new HttpMethod(method), "/api/scheduling" + path)
        {
            Content = method == "GET" ? null : JsonContent.Create(new { serviceCode = "X", serviceName = "Test",
                serviceCategory = "Laboratory", defaultRoomId = Guid.NewGuid(), roomId = Guid.NewGuid(),
                stationCode = "S", stationName = "Bàn", isActive = true })
        }, ct);
        Assert.True(ok.IsSuccessStatusCode, await ok.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Staff_catalog_is_doctor_staff_only_and_never_includes_prices()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        var catalog = new Catalog();
        builder.Services.AddSingleton<IMedicalServiceCatalog>(catalog);
        await using var app = builder.Build();
        app.MapSchedulingEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        const string route = "/api/scheduling/staff/medical-services";
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Staff),
            (Roles.Doctor, Channels.Patient), (Roles.Nurse, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, channel: channel);
            using var forbidden = await client.GetAsync(route, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Doctor);
        using var ok = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.True(catalog.ActiveOnly);
        var json = await ok.Content.ReadAsStringAsync(ct);
        Assert.Contains("serviceCode", json);
        Assert.DoesNotContain("price", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fee", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Catalog : IMedicalServiceCatalog
    {
        public bool ActiveOnly { get; private set; }
        private static MedicalServiceView Service => new(Guid.NewGuid(), "TEST", "Test", "Laboratory", Guid.NewGuid(), true, true, []);
        private static ServiceStationView Station => new(Guid.NewGuid(), Guid.NewGuid(), "TEST", "Bàn", true);
        public Task<CatalogPage<MedicalServiceView>> GetServicesAsync(bool activeOnly, int p, int s, CancellationToken ct) { ActiveOnly = activeOnly; return Task.FromResult(new CatalogPage<MedicalServiceView>([Service], p, s)); }
        public Task<MedicalServiceView> CreateServiceAsync(CreateMedicalService r, Guid u, CancellationToken ct) => Task.FromResult(Service);
        public Task<MedicalServiceView> UpdateServiceAsync(Guid i, UpdateMedicalService r, Guid u, CancellationToken ct) => Task.FromResult(Service);
        public Task SetRoomAsync(Guid i, Guid r, bool a, Guid u, CancellationToken ct) => Task.CompletedTask;
        public Task<CatalogPage<ServiceStationView>> GetStationsAsync(Guid? r, int p, int s, CancellationToken ct) => Task.FromResult(new CatalogPage<ServiceStationView>([Station], p, s));
        public Task<ServiceStationView> CreateStationAsync(CreateServiceStation r, Guid u, CancellationToken ct) => Task.FromResult(Station);
        public Task<ServiceStationView> UpdateStationAsync(Guid i, UpdateServiceStation r, Guid u, CancellationToken ct) => Task.FromResult(Station);
    }
}
