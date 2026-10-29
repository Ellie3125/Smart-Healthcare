using Microsoft.EntityFrameworkCore.Design;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure;

/// <summary>Chỉ dùng cho lệnh dotnet ef (migrations add/script).</summary>
public sealed class ClinicalDbContextFactory : IDesignTimeDbContextFactory<ClinicalDbContext>
{
    public ClinicalDbContext CreateDbContext(string[] args) =>
        new(ModelBuilderExtensions.SqlServerOptions<ClinicalDbContext>(ModelBuilderExtensions.DesignTimeConnectionString, ClinicalDbContext.Schema));
}
