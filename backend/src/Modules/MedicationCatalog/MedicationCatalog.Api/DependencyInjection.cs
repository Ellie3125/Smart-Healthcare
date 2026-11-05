using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MedicationCatalog.Api;

public static class DependencyInjection
{
    public const string Tag = "MedicationCatalog";

    public static IServiceCollection AddMedicationCatalogApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapMedicationCatalogEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/medication-catalog").WithTags(Tag);
}
