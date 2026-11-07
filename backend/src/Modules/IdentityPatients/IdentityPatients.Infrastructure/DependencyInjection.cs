using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityPatientsInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<IdentityPatientsDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityPatientsDbContext.Schema)));
}
