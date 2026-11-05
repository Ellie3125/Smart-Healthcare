using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Scheduling.Api;

public static class DependencyInjection
{
    public const string Tag = "Scheduling";

    public static IServiceCollection AddSchedulingApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/scheduling").WithTags(Tag);
}
