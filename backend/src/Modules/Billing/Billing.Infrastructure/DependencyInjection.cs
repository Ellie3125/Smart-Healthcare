using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BillingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPriceCatalog, EfPriceCatalog>();
        services.AddScoped<IChargeStore, EfChargeStore>();
        services.AddScoped<IPaymentStore, EfPaymentStore>();
        services.AddScoped<PaymentService>();
        services.AddScoped<IBillingModule, BillingModule>();
        return services;
    }
}
