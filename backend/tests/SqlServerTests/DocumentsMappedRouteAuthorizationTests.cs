using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DocumentsReporting.Api;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SqlServerTests;

public sealed class DocumentsMappedRouteAuthorizationTests
{
    [Theory]
    [InlineData("GET", "/api/documents/staff/room/results/00000000-0000-0000-0000-000000000001/files")]
    [InlineData("DELETE", "/api/documents/staff/room/results/00000000-0000-0000-0000-000000000001/files/00000000-0000-0000-0000-000000000002")]
    public async Task Mapped_file_routes_reject_anonymous_wrong_role_and_wrong_channel(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "DocumentsMapping.Tests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, TestDatabase.RequiredConnectionString());
        await using var app = builder.Build(); app.MapDocumentsReportingEndpoints(); await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        using (var request = new HttpRequestMessage(new HttpMethod(method), route))
        using (var response = await client.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Admin, Channels.Staff), (Roles.Doctor, Channels.Staff),
                     (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.Patient, Channels.Patient), (Roles.RoomAccount, Channels.Patient) })
        {
            var token = new JwtSecurityToken("DocumentsMapping.Tests", "DocumentsMapping.Tests",
                [new("sub", Guid.NewGuid().ToString()), new("role", role), new(AppClaims.Channel, channel)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            using var response = await client.SendAsync(request, ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await app.StopAsync(ct);
    }
}
