using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityPatientsInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityPatientsDbContext>(options =>
        {
            options.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityPatientsDbContext.Schema));

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IdentityDbContext>(sp =>
            sp.GetRequiredService<IdentityPatientsDbContext>() as IdentityDbContext
            ?? new IdentityDbContext(sp.GetRequiredService<DbContextOptions<IdentityPatientsDbContext>>()));

        services.AddScoped<Seed.IdentitySeeder>();

        return services;
    }
}
