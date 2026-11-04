using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace IdentityPatients.Infrastructure.Persistence;

public sealed class IdentityPatientsDbContext(DbContextOptions<IdentityPatientsDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityPatientsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
