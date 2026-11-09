using Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<SchedulingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SchedulingDbContext.Schema)));
}
