using System.Diagnostics;
using System.Net.WebSockets;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notifications.Api;
using Notifications.Contracts;
using Xunit;

namespace ArchitectureTests;

// W4-01: hub SignalR + Redis backplane.
public sealed class RealtimeHubTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Redis chết (cổng không có ai nghe): gửi sự kiện không ném lỗi và không treo nghiệp vụ.
    [Fact]
    public async Task Notifier_swallows_failures_when_redis_is_down()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<IHostEnvironment>(new FakeEnvironment())
            .AddNotificationsApi().AddNotificationsRedisBackplane("127.0.0.1:1,connectTimeout=500");
        await using var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<IRealtimeNotifier>();

        var watch = Stopwatch.StartNew();
        await notifier.ToPublicDisplayAsync(Guid.NewGuid(), RealtimeEvents.DisplayBoardUpdated, new { ticketNumber = "001" }, Ct);
        await notifier.ToPatientUserAsync(Guid.NewGuid(), RealtimeEvents.QueueCalled, new { }, Ct);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Gửi mất {watch.Elapsed}");
    }

    // WebSockets (skipNegotiation): staff hub đòi token kênh Staff qua access_token; màn hình công khai không cần token.
    [Fact]
    public async Task Hubs_accept_websocket_only_with_the_right_channel()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = new string('k', 32);
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "RealtimeHubTests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, "Server=unused;Database=none;");
        builder.Services.AddNotificationsApi();
        await using var app = builder.Build();
        app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNotificationsEndpoints();
        await app.StartAsync(Ct);
        var ws = app.Urls.Single().Replace("http", "ws");

        Assert.Equal(WebSocketState.Open, await ConnectAsync($"{ws}/hubs/public-display?roomId={Guid.NewGuid()}"));
        Assert.Null(await ConnectAsync($"{ws}/hubs/staff"));
        Assert.Null(await ConnectAsync($"{ws}/hubs/staff?access_token={Token(Roles.Patient, Channels.Patient)}"));
        Assert.Null(await ConnectAsync($"{ws}/hubs/patient?access_token={Token(Roles.Receptionist, Channels.Staff)}"));
        Assert.Equal(WebSocketState.Open, await ConnectAsync($"{ws}/hubs/staff?access_token={Token(Roles.Receptionist, Channels.Staff)}"));
        Assert.Equal(WebSocketState.Open, await ConnectAsync($"{ws}/hubs/patient?access_token={Token(Roles.Patient, Channels.Patient)}"));
    }

    private static string Token(string role, string channel) => new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
        new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("RealtimeHubTests", "RealtimeHubTests",
            [new("role", role), new("sub", Guid.NewGuid().ToString()), new(AppClaims.Channel, channel)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('k', 32))),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));

    private static async Task<WebSocketState?> ConnectAsync(string url)
    {
        using var client = new ClientWebSocket();
        try
        {
            await client.ConnectAsync(new Uri(url), Ct);
            return client.State;
        }
        catch (WebSocketException)
        {
            return null;
        }
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
