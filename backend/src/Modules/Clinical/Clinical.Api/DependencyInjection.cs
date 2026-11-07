using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Clinical.Api;

public static class DependencyInjection
{
    public const string Tag = "Clinical";

    public static IServiceCollection AddClinicalApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapClinicalEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/clinical").WithTags(Tag);
}
