using System.Xml.Linq;
using Xunit;

namespace ArchitectureTests;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Module_projects_follow_reference_boundaries()
    {
        var modulesDirectory = Path.Combine(FindBackendDirectory(), "src", "Modules");
        var moduleDirectories = Directory.GetDirectories(modulesDirectory);
        Assert.NotEmpty(moduleDirectories);

        var moduleNames = moduleDirectories.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var violations = new List<string>();

        foreach (var moduleDirectory in moduleDirectories)
        {
            var module = Path.GetFileName(moduleDirectory);
            foreach (var projectPath in Directory.EnumerateDirectories(moduleDirectory)
                .SelectMany(layer => Directory.EnumerateFiles(layer, "*.csproj")))
            {
                var project = Path.GetFileNameWithoutExtension(projectPath);
                var xml = XDocument.Load(projectPath);
                foreach (var include in xml.Descendants("ProjectReference").Attributes("Include"))
                {
                    var reference = Path.GetFileNameWithoutExtension(include.Value.Replace('\\', '/'));
                    var targetModule = reference.Split('.')[0];
                    var infrastructure = reference.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase);

                    if (moduleNames.Contains(targetModule) && !targetModule.Equals(module, StringComparison.OrdinalIgnoreCase)
                        && !reference.Equals($"{targetModule}.Contracts", StringComparison.OrdinalIgnoreCase))
                        violations.Add($"{project} → {reference}: module khác chỉ được tham chiếu Contracts.");

                    if (project.EndsWith(".Contracts", StringComparison.Ordinal)
                        && (infrastructure || reference.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase)))
                        violations.Add($"{project} → {reference}: Contracts không được tham chiếu Domain hoặc Infrastructure.");

                    if (project.EndsWith(".Api", StringComparison.Ordinal) && infrastructure)
                        violations.Add($"{project} → {reference}: Api không được tham chiếu Infrastructure.");
                }

                if (project.EndsWith(".Contracts", StringComparison.Ordinal))
                {
                    foreach (var include in xml.Descendants("PackageReference").Attributes("Include")
                        .Where(a => a.Value.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)))
                        violations.Add($"{project} → {include.Value}: Contracts không được tham chiếu EF Core.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static string FindBackendDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartHealthcare.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục backend chứa SmartHealthcare.slnx.");
    }
}
