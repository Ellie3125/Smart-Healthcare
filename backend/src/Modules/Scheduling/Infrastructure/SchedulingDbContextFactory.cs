using Microsoft.EntityFrameworkCore.Design;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure;

/// <summary>Chỉ dùng cho lệnh dotnet ef (migrations add/script).</summary>
public sealed class SchedulingDbContextFactory : IDesignTimeDbContextFactory<SchedulingDbContext>
{
    public SchedulingDbContext CreateDbContext(string[] args) =>
        new(ModelBuilderExtensions.SqlServerOptions<SchedulingDbContext>(ModelBuilderExtensions.DesignTimeConnectionString, SchedulingDbContext.Schema));
}
