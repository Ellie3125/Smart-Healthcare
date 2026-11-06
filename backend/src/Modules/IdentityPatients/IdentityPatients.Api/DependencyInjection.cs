using IdentityPatients.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Api;

public static class DependencyInjection
{
    public static IMvcBuilder AddIdentityPatientsApi(this IMvcBuilder builder) =>
        builder.AddApplicationPart(typeof(AuthController).Assembly);
}

    public const string Tag = "IdentityPatients";

    public static IServiceCollection AddIdentityPatientsApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapIdentityPatientsEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/identity-patients").WithTags(Tag);
}
