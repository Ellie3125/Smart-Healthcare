using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Api;

public static class DependencyInjection
{
    public const string Tag = "Billing";

    public static IServiceCollection AddBillingApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapBillingEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/billing").WithTags(Tag);
}
