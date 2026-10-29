using Microsoft.EntityFrameworkCore.Design;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure;

/// <summary>Chỉ dùng cho lệnh dotnet ef (migrations add/script).</summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(ModelBuilderExtensions.SqlServerOptions<CatalogDbContext>(ModelBuilderExtensions.DesignTimeConnectionString, CatalogDbContext.Schema));
}
