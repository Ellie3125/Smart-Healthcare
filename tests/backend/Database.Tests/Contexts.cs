using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Identity.Infrastructure;
using SmartHealthcare.Catalog.Infrastructure;
using SmartHealthcare.Scheduling.Infrastructure;
using SmartHealthcare.Clinical.Infrastructure;
using SmartHealthcare.Billing.Infrastructure;
using SmartHealthcare.Workflow.Infrastructure;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Database.Tests;

/// <summary>DbContext của các module đã dựng, theo thứ tự migrate.</summary>
public static class Contexts
{
    public static readonly string[] Domains = ["identity", "catalog", "scheduling", "clinical", "billing", "workflow"];

    public static DbContext Create(string domain, string connectionString) => domain switch
    {
        "identity" => new IdentityDbContext(ModelBuilderExtensions.SqlServerOptions<IdentityDbContext>(connectionString, IdentityDbContext.Schema)),
        "catalog" => new CatalogDbContext(ModelBuilderExtensions.SqlServerOptions<CatalogDbContext>(connectionString, CatalogDbContext.Schema)),
        "scheduling" => new SchedulingDbContext(ModelBuilderExtensions.SqlServerOptions<SchedulingDbContext>(connectionString, SchedulingDbContext.Schema)),
        "clinical" => new ClinicalDbContext(ModelBuilderExtensions.SqlServerOptions<ClinicalDbContext>(connectionString, ClinicalDbContext.Schema)),
        "billing" => new BillingDbContext(ModelBuilderExtensions.SqlServerOptions<BillingDbContext>(connectionString, BillingDbContext.Schema)),
        "workflow" => new WorkflowDbContext(ModelBuilderExtensions.SqlServerOptions<WorkflowDbContext>(connectionString, WorkflowDbContext.Schema)),
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };
}
