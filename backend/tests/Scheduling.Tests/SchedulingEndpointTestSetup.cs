using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Billing.Contracts;
using IdentityPatients.Application.Auth;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Shared.Abstractions;

namespace Scheduling.Tests;

internal static class SchedulingEndpointTestSetup
{
    private const string Issuer = "Scheduling.Tests";

    public static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = Issuer;
        builder.Services.AddIdentityPatientsAuth(builder.Configuration);
        builder.Services.RemoveAll<IAuthService>();
        builder.Services.AddOpenApi();
        builder.Services.AddSchedulingApplication().AddSchedulingApi();
        builder.Services.AddSingleton<IDoctorDayStore, DoctorDayEndpointTests.Store>();
        builder.Services.AddSingleton<IDoctorSearchQuotaReader>(new DoctorSearchTests.Quotas([]));
        builder.Services.AddSingleton<IBillingModule, DoctorSearchTests.SingleQuoteBilling>();
        builder.Services.AddSingleton<IDbClock>(new DoctorSearchTests.FakeDbClock(DoctorSearchTests.CutoffUtc.AddSeconds(-1)));
        return builder;
    }

    public static void Authorize(HttpClient client, IConfiguration configuration, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(Issuer, Issuer, [new Claim("role", role)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}
