using DocumentsReporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentsReporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsReportingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<DocumentsDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DocumentsDbContext.Schema)));
}
