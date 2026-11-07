using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentsReporting.Api;

public static class DependencyInjection
{
    public const string Tag = "DocumentsReporting";

    public static IServiceCollection AddDocumentsReportingApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapDocumentsReportingEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/documents-reporting").WithTags(Tag);
}
