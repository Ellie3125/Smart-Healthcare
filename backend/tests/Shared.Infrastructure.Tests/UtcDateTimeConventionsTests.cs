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

namespace Shared.Infrastructure.Tests;

public class UtcDateTimeConventionsTests
{
    private static readonly UtcDateTimeConverter Converter = new();

    [Fact]
    public void Write_accepts_utc_value_unchanged()
    {
        var value = new DateTime(2026, 11, 2, 1, 30, 0, DateTimeKind.Utc);

        Assert.Equal(value, Converter.ConvertToProvider(value));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Write_rejects_non_utc_value(DateTimeKind kind)
    {
        var value = new DateTime(2026, 11, 2, 8, 30, 0, kind);

        Assert.Throws<ArgumentException>(() => Converter.ConvertToProvider(value));
    }

    [Fact]
    public void Read_marks_value_as_utc_without_shifting()
    {
        var stored = new DateTime(2026, 11, 2, 1, 30, 0, DateTimeKind.Unspecified);

        var read = (DateTime)Converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(stored.Ticks, read.Ticks);
    }

    public static TheoryData<Type> Contexts =>
    [
        typeof(IdentityPatientsDbContext), typeof(SchedulingDbContext), typeof(ClinicalDbContext), typeof(MedicationDbContext),
        typeof(BillingDbContext), typeof(WorkflowDbContext), typeof(DocumentsDbContext), typeof(SharedDbContext),
    ];

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Every_DateTime_property_uses_utc_converter(Type contextType)
    {
        // Chỉ dựng model, không mở kết nối.
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        builder.UseSqlServer("Data Source=unused;Initial Catalog=unused");
        using var context = (DbContext)Activator.CreateInstance(contextType, builder.Options)!;

        var dateTimeProperties = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .ToList();

        Assert.All(dateTimeProperties, p => Assert.IsType<UtcDateTimeConverter>(p.GetValueConverter()));
    }
}
