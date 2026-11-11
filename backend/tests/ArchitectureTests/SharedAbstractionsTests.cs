using System.Reflection;
using Xunit;

namespace ArchitectureTests;

// CLA-W1-02: Contracts của mọi module tham chiếu Shared.Abstractions, nên nó chỉ được dùng BCL;
// kéo EF Core hay DI vào đây thì luật "Contracts không tham chiếu EF" mất tác dụng.
public class SharedAbstractionsTests
{
    [Fact]
    public void Shared_abstractions_references_only_BCL()
    {
        var violations = Assembly.Load(new AssemblyName("Shared.Abstractions")).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !(n == "System.Runtime" ||n.StartsWith("System.", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(violations.Length == 0, $"Shared.Abstractions tham chiếu ngoài BCL: {string.Join(", ", violations)}");
    }
}
