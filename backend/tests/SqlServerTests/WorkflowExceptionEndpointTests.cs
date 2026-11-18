using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api;
using Notifications.Application.Exceptions;
using Xunit;

namespace SqlServerTests;

public sealed class WorkflowExceptionEndpointTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Monitor_and_retry_require_admin_staff_and_use_authenticated_actor(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Workflow.Tests";
        builder.Services.AddIdentityPatientsAuth(builder.Configuration);
        builder.Services.AddNotificationsApi();
        var monitor = new Monitor();
        builder.Services.AddSingleton<IWorkflowExceptionMonitor>(monitor);
        await using var app = builder.Build();
        app.MapNotificationsEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var message = Guid.NewGuid();
        var path = "/api/workflow/admin/exceptions" + (method == "POST" ? $"/outbox/{message}/retry" : "");
        using var anonymous = await client.SendAsync(new(new(method), path), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Patient),
            (Roles.Doctor, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            Authorize(client, app.Configuration, Guid.NewGuid(), role, channel);
            using var forbidden = await client.SendAsync(new(new(method), path), ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        var actor = Guid.NewGuid();
        Authorize(client, app.Configuration, actor, Roles.Admin, Channels.Staff);
        using var request = new HttpRequestMessage(new(method), path)
        {
            Content = method == "POST" ? JsonContent.Create(new { resolutionNote = "Đã kiểm nguyên nhân" }) : null
        };
        request.Headers.Add("Idempotency-Key", "test-key");
        using var ok = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        if (method == "POST")
        {
            Assert.Equal(actor, monitor.Actor);
            Assert.Equal(message, monitor.MessageId);
            foreach (var (result, status) in new[] { (RetryExceptionResult.InvalidInput, HttpStatusCode.BadRequest),
                (RetryExceptionResult.NotFound, HttpStatusCode.NotFound), (RetryExceptionResult.Conflict, HttpStatusCode.Conflict),
                (RetryExceptionResult.InProgress, HttpStatusCode.Conflict) })
            {
                monitor.Outcome = result;
                using var error = await client.PostAsJsonAsync(path, new { resolutionNote = "Valid" }, ct);
                Assert.Equal(status, error.StatusCode);
                Assert.Contains(result.ToString(), await error.Content.ReadAsStringAsync(ct));
            }
            Authorize(client, app.Configuration, Guid.Empty, Roles.Admin, Channels.Staff);
            using var noActor = await client.PostAsJsonAsync(path, new { resolutionNote = "Valid" }, ct);
            Assert.Equal(HttpStatusCode.Unauthorized, noActor.StatusCode);
        }
        else
        {
            Assert.Contains("unavailableSources", await ok.Content.ReadAsStringAsync(ct));
            using var invalid = await client.GetAsync(path + "?take=101", ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    private static void Authorize(HttpClient client, IConfiguration config, Guid actor, string role, string channel)
    {
        var token = new JwtSecurityToken("Workflow.Tests", "Workflow.Tests",
            [new("sub", actor.ToString()), new("role", role), new(AppClaims.Channel, channel)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class Monitor : IWorkflowExceptionMonitor
    {
        public Guid Actor { get; private set; }
        public Guid MessageId { get; private set; }
        public RetryExceptionResult Outcome { get; set; } = RetryExceptionResult.Retried;
        public Task<WorkflowExceptionList> GetAsync(int take, CancellationToken ct) => Task.FromResult(new WorkflowExceptionList([], ["Billing.PaidUnallocated"]));
        public Task<RetryExceptionResult> RetryOutboxAsync(Guid m, Guid u, string n, string k, CancellationToken ct)
        {
            Actor = u; MessageId = m;
            return Task.FromResult(Outcome);
        }
    }
}
