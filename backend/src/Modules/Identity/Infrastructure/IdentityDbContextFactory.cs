using Microsoft.EntityFrameworkCore.Design;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure;

/// <summary>Chỉ dùng cho lệnh dotnet ef (migrations add/script).</summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(ModelBuilderExtensions.SqlServerOptions<IdentityDbContext>(ModelBuilderExtensions.DesignTimeConnectionString, IdentityDbContext.Schema));
}
