using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications.Api;

public static class DependencyInjection
{
    public const string Tag = "Notifications";

    public static IServiceCollection AddNotificationsApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/notifications").WithTags(Tag);
}
