using MedicationCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicationCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMedicationCatalogInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<MedicationDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MedicationDbContext.Schema)));
}
