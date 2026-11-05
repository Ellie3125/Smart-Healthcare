using Clinical.Application;
using Clinical.Contracts;
using Clinical.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinical.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClinicalDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ClinicalDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEncounterStore, EfEncounterStore>();
        services.AddScoped<IServiceOrderStore, EfServiceOrderStore>();
        services.AddScoped<IPrescriptionStore, EfPrescriptionStore>();
        services.AddScoped<IClinicalModule, ClinicalModule>();

        return services;
    }
}
