using Billing.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence;
using DocumentsReporting.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Persistence;
using MedicationCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Infrastructure.Persistence;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

// Model EF hiện tại phải khớp snapshot migration của từng DbContext (thay đổi schema chỉ qua migration mới).
public class PendingModelChangesTests
{
    public static TheoryData<Type> Contexts =>
    [
        typeof(IdentityPatientsDbContext), typeof(SchedulingDbContext), typeof(ClinicalDbContext), typeof(MedicationDbContext),
        typeof(BillingDbContext), typeof(WorkflowDbContext), typeof(DocumentsDbContext), typeof(SharedDbContext),
    ];

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Model_has_no_pending_changes(Type contextType)
    {
        // Không mở kết nối: chỉ so model với snapshot.
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        builder.UseSqlServer("Data Source=unused;Initial Catalog=unused");
        using var context = (DbContext)Activator.CreateInstance(contextType, builder.Options)!;

        Assert.False(context.Database.HasPendingModelChanges(), $"{contextType.Name} có thay đổi model chưa có migration");
    }
}
