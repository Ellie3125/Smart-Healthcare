using Microsoft.EntityFrameworkCore.Design;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Workflow.Infrastructure;

/// <summary>Chỉ dùng cho lệnh dotnet ef (migrations add/script).</summary>
public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args) =>
        new(ModelBuilderExtensions.SqlServerOptions<WorkflowDbContext>(ModelBuilderExtensions.DesignTimeConnectionString, WorkflowDbContext.Schema));
}
