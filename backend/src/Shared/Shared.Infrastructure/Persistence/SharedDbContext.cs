using Microsoft.EntityFrameworkCore;

namespace Shared.Infrastructure.Persistence;

public sealed class SharedDbContext(DbContextOptions<SharedDbContext> options) : DbContext(options)
{
    public const string Schema = "shared";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SharedDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
