using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using MedicationCatalogEntity = MedicationCatalog.Domain.Entities.MedicationCatalog;

namespace MedicationCatalog.Infrastructure.Persistence;

public sealed class MedicationDbContext(DbContextOptions<MedicationDbContext> options) : DbContext(options)
{
    public const string Schema = "medication";

    public DbSet<MedicationCatalogEntity> MedicationCatalogs => Set<MedicationCatalogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
