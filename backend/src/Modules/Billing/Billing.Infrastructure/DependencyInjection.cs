using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<BillingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)));
}
