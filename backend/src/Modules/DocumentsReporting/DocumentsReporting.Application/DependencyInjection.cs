using DocumentsReporting.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentsReporting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsReportingApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentsModule, DocumentsService>();
        services.AddScoped<Reports.ReportSummaryService>();
        return services;
    }
}
