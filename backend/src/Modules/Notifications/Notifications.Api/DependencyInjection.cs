using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using StackExchange.Redis;

namespace Notifications.Api;

public static class DependencyInjection
{
    public const string Tag = "Notifications";

    /// <summary>SignalR + IRealtimeNotifier. Api và Worker cùng gọi; backplane thêm bằng <see cref="AddNotificationsRedisBackplane"/>.</summary>
    public static IServiceCollection AddNotificationsApi(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
        // 10 lượt chat/phút mỗi tài khoản (contract chatbot §3.1); host bật middleware bằng UseRateLimiter.
        services.AddRateLimiter(options => options.AddPolicy<string, PatientChatRateLimitPolicy>(PatientChatEndpoints.RateLimitPolicy));
        // WebSockets không gửi được header Authorization: client gửi token qua query access_token, chỉ nhận ở /hubs.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events?.OnMessageReceived;
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = async context =>
            {
                if (previous is not null)
                    await previous(context);
                if (string.IsNullOrEmpty(context.Token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                    && context.Request.Query["access_token"] is { Count: 1 } token)
                    context.Token = token[0];
            };
        });
        return services;
    }

    /// <summary>
    /// Redis backplane cho BE1, BE2 và Worker (Giải pháp §8.6, không sticky session). Không có chuỗi kết nối thì chạy một
    /// instance không backplane (máy dev). AbortOnConnectFail = false: Redis chết thì Api vẫn khởi động, nghiệp vụ vẫn chạy.
    /// </summary>
    public static IServiceCollection AddNotificationsRedisBackplane(this IServiceCollection services, string? redisConnection)
    {
        if (string.IsNullOrWhiteSpace(redisConnection))
            return services;
        services.AddSignalR().AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("SmartHealthcare");
            options.Configuration.AbortOnConnectFail = false;
        });
        return services;
    }

    public static RouteGroupBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Client dùng WebSockets với skipNegotiation nên chỉ bật transport WebSockets.
        app.MapHub<StaffHub>("/hubs/staff", o => o.Transports = HttpTransportType.WebSockets);
        app.MapHub<PatientHub>("/hubs/patient", o => o.Transports = HttpTransportType.WebSockets);
        app.MapHub<PublicDisplayHub>("/hubs/public-display", o => o.Transports = HttpTransportType.WebSockets);
        app.MapWorkflowExceptionRoutes();
        var group = app.MapGroup("/api/notifications").WithTags(Tag);
        group.MapPatientNotificationRoutes();
        group.MapPatientChatRoutes();
        return group;
    }
}
