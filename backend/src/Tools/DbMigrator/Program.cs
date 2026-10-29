using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Identity.Infrastructure;
using SmartHealthcare.Catalog.Infrastructure;
using SmartHealthcare.Scheduling.Infrastructure;
using SmartHealthcare.Clinical.Infrastructure;
using SmartHealthcare.Billing.Infrastructure;
using SmartHealthcare.Workflow.Infrastructure;
using SmartHealthcare.Shared.Persistence;

// Tác vụ migrate riêng cho quy trình release (DB-03); BE1/BE2 không tự migrate.
// Dùng tài khoản migrate, tách khỏi tài khoản runtime; connection string chỉ lấy từ biến môi trường.
var connectionString = Environment.GetEnvironmentVariable("SH_MIGRATOR_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thiếu biến môi trường SH_MIGRATOR_CONNECTION.");
    return 1;
}

// EF Core 9+ MigrateAsync tự lấy sp_getapplock mức session cho từng context, nên không bọc thêm khóa.
DbContext[] contexts =
[
    new IdentityDbContext(ModelBuilderExtensions.SqlServerOptions<IdentityDbContext>(connectionString, IdentityDbContext.Schema)),
    new CatalogDbContext(ModelBuilderExtensions.SqlServerOptions<CatalogDbContext>(connectionString, CatalogDbContext.Schema)),
    new SchedulingDbContext(ModelBuilderExtensions.SqlServerOptions<SchedulingDbContext>(connectionString, SchedulingDbContext.Schema)),
    new ClinicalDbContext(ModelBuilderExtensions.SqlServerOptions<ClinicalDbContext>(connectionString, ClinicalDbContext.Schema)),
    new BillingDbContext(ModelBuilderExtensions.SqlServerOptions<BillingDbContext>(connectionString, BillingDbContext.Schema)),
    new WorkflowDbContext(ModelBuilderExtensions.SqlServerOptions<WorkflowDbContext>(connectionString, WorkflowDbContext.Schema)),
];

foreach (var context in contexts)
{
    await using (context)
    {
        Console.WriteLine($"Migrate {context.GetType().Name}...");
        await context.Database.MigrateAsync();
    }
}

Console.WriteLine("Xong.");
return 0;
