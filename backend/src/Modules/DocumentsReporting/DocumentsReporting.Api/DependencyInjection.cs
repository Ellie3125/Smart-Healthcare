using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentsReporting.Api;

public static class DependencyInjection
{
    public const string Tag = "DocumentsReporting";

    public static IServiceCollection AddDocumentsReportingApi(this IServiceCollection services) => services;

    public static void MapDocumentsReportingEndpoints(this IEndpointRouteBuilder app)
    {
        DocumentsEndpoints.MapEndpoints(app);
    }
}
