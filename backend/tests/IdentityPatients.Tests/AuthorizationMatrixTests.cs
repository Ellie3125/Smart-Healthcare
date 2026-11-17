using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPatients.Api;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Application.DoctorDays;
using Shared.Infrastructure;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

/// <summary>Mỗi vai trò chỉ vào khu vực của mình; policy kiểm cả vai trò và kênh (D5, D6).</summary>
public sealed class AuthorizationMatrixTests
{
    private static readonly string[] AllRoles = [Roles.Admin, Roles.Receptionist, Roles.Nurse, Roles.Doctor, Roles.RoomAccount, Roles.Patient];
    private static readonly string[] AllChannels = [Channels.Staff, Channels.Patient];

    private static ClaimsPrincipal Principal(string role, string channel) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim(AppClaims.Channel, channel)], "test"));

    public static TheoryData<string, string, string> PolicyMatrix()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var policy in Policies.RoleOf.Keys.Append(Policies.AnyChannel).Append(Policies.ReceptionistOrAdmin))
        foreach (var role in AllRoles)
        foreach (var channel in AllChannels)
            data.Add(policy, role, channel);
        return data;
    }

    // Đủ 6 vai trò × 2 kênh × 8 policy: chỉ đúng vai trò của policy với đúng kênh của vai trò đó được qua.
    // Gồm cả khu vực chưa có endpoint (DoctorOnly, NurseOnly, RoomAccountOnly): Admin chỉ thêm ReceptionistOrAdmin và AdminOnly.
    [Theory]
    [MemberData(nameof(PolicyMatrix))]
    public async Task Policy_AllowsOnlyItsRoleOnItsChannel(string policy, string role, string channel)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = new string('k', 32) }).Build();
        await using var provider = new ServiceCollection().AddLogging().AddIdentityPatientsAuth(configuration).BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var allowed = (await authorization.AuthorizeAsync(Principal(role, channel), policy)).Succeeded;

        var expected = channel == Policies.ChannelOf(role)
            && (policy == Policies.AnyChannel
                || (policy == Policies.ReceptionistOrAdmin && role is Roles.Receptionist or Roles.Admin)
                || (Policies.RoleOf.TryGetValue(policy, out var requiredRole) && requiredRole == role));
        Assert.Equal(expected, allowed);
    }

    public class ThrowingStore : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Endpoint không được chạy tới store trong test phân quyền.");
    }

    // HTTP thật: token từng vai trò, gọi một endpoint đại diện của mỗi khu vực đang có.
    [Fact]
    public async Task Http_EachRoleReachesOnlyItsArea()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = TestDatabase.RequiredConnectionString();
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = signingKey;
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "IdentityPatients.Tests";
        builder.Services.AddControllers().AddIdentityPatientsApi();
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, connectionString)
            .AddSharedInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>();
        builder.Services.AddSchedulingApplication().AddSchedulingApi();
        builder.Services.AddSingleton(DispatchProxy.Create<IDoctorDayStore, ThrowingStore>());
        builder.Services.AddSingleton(DispatchProxy.Create<Scheduling.Application.DoctorSearch.IDoctorSearchQuotaReader, ThrowingStore>());
        builder.Services.AddSingleton(DispatchProxy.Create<Billing.Contracts.IBillingModule, ThrowingStore>());
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapSchedulingEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        string Token(string role, string channel) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "IdentityPatients.Tests", "IdentityPatients.Tests",
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role), new Claim(AppClaims.Channel, channel)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)));

        async Task<HttpStatusCode> Call(HttpMethod method, string url, string? role, string? channel = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (role is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role, channel ?? Policies.ChannelOf(role)));
            }

            using var response = await client.SendAsync(request, ct);
            return response.StatusCode;
        }

        var areas = new (HttpMethod Method, string Url, string[] AllowedRoles)[]
        {
            (HttpMethod.Get, "/api/staff/admin/specialties", [Roles.Admin]),
            (HttpMethod.Get, "/api/staff/admin/rooms/" + Guid.NewGuid() + "/sessions", [Roles.Admin]),
            (HttpMethod.Post, "/api/scheduling/admin/doctor-days/" + Guid.NewGuid() + "/2099-01-01", [Roles.Admin]),
            (HttpMethod.Get, "/api/staff/verifications", [Roles.Receptionist, Roles.Admin]),
            (HttpMethod.Put, "/api/staff/patients/" + Guid.NewGuid(), [Roles.Receptionist, Roles.Admin]),
            (HttpMethod.Get, "/api/patient/profiles", [Roles.Patient]),
            (HttpMethod.Get, "/api/scheduling/patient/doctors?serviceDate=2099-01-01", [Roles.Patient]),
        };

        foreach (var (method, url, allowedRoles) in areas)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await Call(method, url, null));
            foreach (var role in AllRoles.Where(r => !allowedRoles.Contains(r)))
            {
                Assert.True(HttpStatusCode.Forbidden == await Call(method, url, role), $"{role} gọi {url} phải bị 403");
            }

            // Đúng vai trò nhưng token sai kênh cũng bị chặn.
            var primaryRole = allowedRoles[0];
            var wrongChannel = Policies.ChannelOf(primaryRole) == Channels.Staff ? Channels.Patient : Channels.Staff;
            Assert.True(HttpStatusCode.Forbidden == await Call(method, url, primaryRole, wrongChannel), $"{primaryRole} kênh {wrongChannel} gọi {url} phải bị 403");
        }

        // Đúng vai trò, đúng kênh: qua được phân quyền (các endpoint GET chỉ đọc DB test).
        Assert.Equal(HttpStatusCode.OK, await Call(HttpMethod.Get, "/api/staff/admin/specialties", Roles.Admin));
        Assert.Equal(HttpStatusCode.OK, await Call(HttpMethod.Get, "/api/staff/verifications", Roles.Receptionist));
        Assert.Equal(HttpStatusCode.OK, await Call(HttpMethod.Get, "/api/staff/verifications", Roles.Admin));
        Assert.Equal(HttpStatusCode.OK, await Call(HttpMethod.Get, "/api/patient/profiles", Roles.Patient));

        // /me: mọi vai trò với đúng kênh; token Patient mang kênh Staff bị 403.
        foreach (var role in AllRoles)
        {
            Assert.Equal(HttpStatusCode.OK, await Call(HttpMethod.Get, "/me", role));
        }
        Assert.Equal(HttpStatusCode.Forbidden, await Call(HttpMethod.Get, "/me", Roles.Patient, Channels.Staff));

        // Lỗi của module có extension code (docs/conventions.md §4).
        using var login = await client.PostAsync("/auth/staff/login",
            new StringContent("""{"userName":"khong-ton-tai","password":"sai"}""", Encoding.UTF8, "application/json"), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var problem = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct));
        Assert.Equal("InvalidCredentials", problem.RootElement.GetProperty("code").GetString());
    }
}
