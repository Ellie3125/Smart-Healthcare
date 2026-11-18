using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Clinical.Infrastructure.Persistence;

public sealed class ClinicalDbContext(DbContextOptions<ClinicalDbContext> options) : DbContext(options)
{
    public const string Schema = "clinical";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicalDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
