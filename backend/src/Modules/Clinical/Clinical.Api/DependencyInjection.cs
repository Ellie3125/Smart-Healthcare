using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Clinical.Application.Vitals;

namespace Clinical.Api;

public static class DependencyInjection
{
    public const string Tag = "Clinical";

    public static IServiceCollection AddClinicalApi(this IServiceCollection services) =>
        services.AddScoped<INurseStaffDirectory, IdentityNurseStaffDirectory>()
            .AddScoped<Clinical.Application.Results.ClsPublicationService>();

    public static RouteGroupBuilder MapClinicalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = ClinicalEndpoints.MapEndpoints(app);
        group.MapNurseVitalsRoutes();
        group.MapRoomResultRoutes();
        return group;
    }
}
