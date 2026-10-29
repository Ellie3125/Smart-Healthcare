using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure;

public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    public const string Schema = "scheduling";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
}
