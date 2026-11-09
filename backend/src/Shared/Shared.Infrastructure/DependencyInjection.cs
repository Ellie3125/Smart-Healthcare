using Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SharedDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SharedDbContext.Schema)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OutboxWriter>();
        return services;
    }
}
