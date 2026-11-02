using Clinical.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinical.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<ClinicalDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ClinicalDbContext.Schema)));
}
