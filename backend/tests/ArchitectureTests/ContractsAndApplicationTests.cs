using System.Reflection;
using Xunit;

namespace ArchitectureTests;

// ARCH-02, MOD-02: Contracts chỉ chứa interface/DTO/event, không phụ thuộc implementation;
// Application không phụ thuộc Infrastructure hay EF Core/ASP.NET Core.
public class ContractsAndApplicationTests
{
    private static string[] ReferencedNames(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName)).GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Theory]
    [InlineData("Billing.Contracts")]
    [InlineData("Clinical.Contracts")]
    [InlineData("Scheduling.Contracts")]
    public void Contracts_reference_only_BCL_and_shared_abstractions(string assembly)
    {
        var violations = ReferencedNames(assembly)
            .Where(n => !(n is "System.Runtime" or "Shared.Abstractions" || n.StartsWith("System.", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(violations.Length == 0, $"{assembly} tham chiếu ngoài phạm vi contract: {string.Join(", ", violations)}");
    }

    [Theory]
    [InlineData("Billing")]
    [InlineData("Clinical")]
    public void Application_does_not_reference_infrastructure_or_frameworks(string module)
    {
        var violations = ReferencedNames($"{module}.Application")
            .Where(n => n == $"{module}.Infrastructure"
                     || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToArray();

        Assert.True(violations.Length == 0, $"{module}.Application tham chiếu hạ tầng: {string.Join(", ", violations)}");
        Assert.Contains($"{module}.Contracts", ReferencedNames($"{module}.Application"));
    }
}
