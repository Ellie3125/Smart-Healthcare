using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Persistence;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public const string Schema = "workflow";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
