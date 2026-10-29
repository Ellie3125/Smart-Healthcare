using System.Reflection;

namespace SmartHealthcare.ArchitectureTests;

/// <summary>
/// Kiểm chiều phụ thuộc theo backend-solid-architecture.md (ARCH-02) và ranh giới module (MOD-01, MOD-02).
/// Dựa trên assembly reference thực sự có trong metadata sau khi biên dịch, nên chỉ bắt được phụ thuộc kiểu đã dùng.
/// </summary>
public class ModuleDependencyTests
{
    private static readonly string[] Modules = ["Identity", "Patients"];

    private static string[] ProjectAssemblyNames() =>
        Directory.GetFiles(AppContext.BaseDirectory, "SmartHealthcare.*.dll")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => !n.EndsWith("Tests", StringComparison.Ordinal))
            .ToArray();

    public static TheoryData<string> AllProjectAssemblies => new(ProjectAssemblyNames());

    private static string[] ReferencesOf(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void All_module_layers_are_built_into_the_host()
    {
        var names = ProjectAssemblyNames().ToHashSet();
        foreach (var module in Modules)
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Presentation" })
            Assert.Contains($"SmartHealthcare.{module}.{layer}", names);
    }

    [Fact]
    public void SharedKernel_depends_on_nothing_from_the_solution_or_frameworks()
    {
        var refs = ReferencesOf("SmartHealthcare.SharedKernel");
        Assert.DoesNotContain(refs, r => r.StartsWith("SmartHealthcare.", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, IsFrameworkForOuterLayers);
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Patients")]
    public void Domain_only_uses_SharedKernel(string module)
    {
        var refs = ReferencesOf($"SmartHealthcare.{module}.Domain");
        Assert.All(refs.Where(r => r.StartsWith("SmartHealthcare.", StringComparison.Ordinal)),
            r => Assert.Equal("SmartHealthcare.SharedKernel", r));
        Assert.DoesNotContain(refs, IsFrameworkForOuterLayers);
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Patients")]
    public void Application_does_not_depend_on_infrastructure_presentation_or_frameworks(string module)
    {
        var refs = ReferencesOf($"SmartHealthcare.{module}.Application");
        Assert.DoesNotContain(refs, r => r.EndsWith(".Infrastructure", StringComparison.Ordinal) || r.EndsWith(".Presentation", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, IsFrameworkForOuterLayers);
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Patients")]
    public void Presentation_does_not_touch_persistence(string module)
    {
        var refs = ReferencesOf($"SmartHealthcare.{module}.Presentation");
        Assert.DoesNotContain(refs, r => r.EndsWith(".Infrastructure", StringComparison.Ordinal) || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Contracts_do_not_expose_module_internals()
    {
        var refs = ReferencesOf("SmartHealthcare.Identity.Contracts");
        Assert.DoesNotContain(refs, r => r.StartsWith("SmartHealthcare.", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllProjectAssemblies))]
    public void Modules_only_reach_other_modules_through_contracts(string assemblyName)
    {
        var owner = Modules.FirstOrDefault(m => assemblyName.StartsWith($"SmartHealthcare.{m}.", StringComparison.Ordinal));
        if (owner is null) return;

        foreach (var other in Modules.Where(m => m != owner))
        {
            var crossing = ReferencesOf(assemblyName)
                .Where(r => r.StartsWith($"SmartHealthcare.{other}.", StringComparison.Ordinal) && r != $"SmartHealthcare.{other}.Contracts");
            Assert.Empty(crossing);
        }
    }

    [Fact]
    public void Host_and_modules_do_not_reference_the_migrations_project()
    {
        // DB-03: API không tự migrate khi khởi động; migration của backend/src/Database chạy riêng bằng dotnet ef.
        Assert.DoesNotContain("SmartHealthcare.Database", ProjectAssemblyNames());
        Assert.All(ProjectAssemblyNames(), name => Assert.DoesNotContain("SmartHealthcare.Database", ReferencesOf(name)));
    }

    private static bool IsFrameworkForOuterLayers(string reference) =>
        reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
        || reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
        || reference.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal);
}
