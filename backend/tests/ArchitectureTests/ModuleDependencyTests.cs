using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace ArchitectureTests;

// Kiểm luật tham chiếu theo Giải pháp kỹ thuật v2.8 §8.2.2 (MOD-01, MOD-04) ở mức assembly đã biên dịch.
public class ModuleDependencyTests
{
    private static readonly string[] Modules =
        ["IdentityPatients", "Scheduling", "Clinical", "MedicationCatalog", "Billing", "Notifications", "DocumentsReporting"];

    public static TheoryData<string> ModuleNames => [.. Modules];

    private static string[] ReferencedNames(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName)).GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    private static string[] ModuleAssemblies(string module) => [$"{module}.Domain", $"{module}.Infrastructure"];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_does_not_reference_another_modules_Domain_or_Infrastructure(string module)
    {
        var forbidden = Modules.Where(m => m != module).SelectMany(ModuleAssemblies).ToHashSet();

        foreach (var assembly in ModuleAssemblies(module))
        {
            var violations = ReferencedNames(assembly).Where(forbidden.Contains).ToArray();
            Assert.True(violations.Length == 0, $"{assembly} tham chiếu module khác: {string.Join(", ", violations)}");
        }

        // Đối chứng: reflection thấy được tham chiếu hợp lệ Infrastructure → Domain của chính module.
        Assert.Contains($"{module}.Domain", ReferencedNames($"{module}.Infrastructure"));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_does_not_reference_EF_Core_or_ASP_NET_Core(string module)
    {
        var violations = ReferencedNames($"{module}.Domain")
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToArray();

        Assert.True(violations.Length == 0, $"{module}.Domain tham chiếu hạ tầng: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Worker_does_not_reference_business_module_Domain_or_Infrastructure()
    {
        var forbidden = Modules.SelectMany(ModuleAssemblies).ToHashSet();

        var violations = ReferencedNames("SmartHealthcare.Worker").Where(forbidden.Contains).ToArray();

        Assert.True(violations.Length == 0, $"SmartHealthcare.Worker tham chiếu module nghiệp vụ: {string.Join(", ", violations)}");
        Assert.Contains("Hangfire.Core", ReferencedNames("SmartHealthcare.Worker"));
    }

    [Fact]
    public void SharedDbContext_contains_only_the_four_infrastructure_tables()
    {
        // Chỉ dựng model, không mở kết nối.
        var options = new DbContextOptionsBuilder<SharedDbContext>().UseSqlServer("Data Source=unused;Initial Catalog=unused").Options;
        using var context = new SharedDbContext(options);

        var entities = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).Order().ToArray();

        Assert.Equal(["ApiIdempotencyRecord", "AuditLog", "OutboxMessage", "ProcessedEvent"], entities);
    }
}
