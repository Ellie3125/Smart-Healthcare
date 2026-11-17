using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Application.SessionAdministration;
using Scheduling.Infrastructure;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

public sealed class SessionAdministrationEndpointTests
{
    [Theory]
    [InlineData("/admin/doctor-days/11111111-1111-1111-1111-111111111111/2099-03-01/replace-doctor", false)]
    [InlineData("/admin/doctor-days/11111111-1111-1111-1111-111111111111/2099-03-01/cancel-session", false)]
    [InlineData("/admin/rooms/11111111-1111-1111-1111-111111111111/disable", false)]
    [InlineData("/staff/call-list", true)]
    public async Task Routes_enforce_roles_and_channel_and_take_actor_from_the_session(string path, bool read)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Session.Tests";
        builder.Services.AddIdentityPatientsAuth(builder.Configuration);
        builder.Services.AddSharedInfrastructure("Server=unused;Database=unused;").AddSchedulingInfrastructure("Server=unused;Database=unused;").AddSchedulingApplication().AddSchedulingApi();
        var fake = new Admin(); builder.Services.AddSingleton<ISessionAdministration>(fake);
        await using var app = builder.Build(); app.MapSchedulingEndpoints(); await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var route = "/api/scheduling" + path;
        using var anonymous = await client.SendAsync(new(read ? HttpMethod.Get : HttpMethod.Post, route), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Patient), (Roles.Doctor, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            Authorize(client, app.Configuration, Guid.NewGuid(), role, channel);
            using var forbidden = await client.SendAsync(new(read ? HttpMethod.Get : HttpMethod.Post, route), ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        var actor = Guid.NewGuid(); Authorize(client, app.Configuration, actor, Roles.Admin, Channels.Staff);
        using var ok = read ? await client.GetAsync(route, ct) : await client.PostAsJsonAsync(route,
            new { replacementDoctorId = Guid.NewGuid(), reason = "Thay ca", userId = Guid.NewGuid() }, ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        if (!read) Assert.Equal(actor, fake.Actor);
        Authorize(client, app.Configuration, actor, Roles.Receptionist, Channels.Staff);
        using var receptionist = read ? await client.GetAsync(route, ct) : await client.PostAsJsonAsync(route, new { reason = "Test" }, ct);
        Assert.Equal(read ? HttpStatusCode.OK : HttpStatusCode.Forbidden, receptionist.StatusCode);
    }

    private static void Authorize(HttpClient client, IConfiguration config, Guid user, string role, string channel)
    {
        var jwt = new JwtSecurityToken("Session.Tests", "Session.Tests", [new("sub", user.ToString()), new("role", role), new(AppClaims.Channel, channel)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }
    private sealed class Admin : ISessionAdministration
    {
        public Guid Actor { get; private set; }
        private static SessionAdminView View => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2099, 3, 1), 1, 1, 2, true, "Scheduled");
        public Task<SessionAdminView> ReplaceDoctorAsync(Guid d, DateOnly date, Guid r, Guid u, string why, CancellationToken ct) { Actor = u; return Task.FromResult(View); }
        public Task<SessionAdminView> CancelSessionAsync(Guid d, DateOnly date, Guid u, string why, CancellationToken ct) { Actor = u; return Task.FromResult(View); }
        public Task<RoomDisabledView> DisableRoomAsync(Guid r, Guid u, string why, CancellationToken ct) { Actor = u; return Task.FromResult(new RoomDisabledView(r, 1, 1)); }
        public Task<StaffCallPage> GetCallListAsync(int p, int s, CancellationToken ct) => Task.FromResult(new StaffCallPage([], p, s, false, false));
    }
}
