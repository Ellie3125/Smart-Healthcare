using System.Reflection;
using IdentityPatients.Contracts;
using Xunit;

namespace ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void IdentityPatientsContracts_ShouldNotReferenceDomainOrInfrastructure()
    {
        var assembly = typeof(IIdentityPatientsModule).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name?.Contains("Domain") == true);
        Assert.DoesNotContain(referencedAssemblies, a => a.Name?.Contains("Infrastructure") == true);
    }

    [Fact]
    public void DocumentsReportingContracts_ShouldNotReferenceDomainOrInfrastructure()
    {
        var assembly = typeof(DocumentsReporting.Contracts.IDocumentsModule).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name?.Contains("Domain") == true);
        Assert.DoesNotContain(referencedAssemblies, a => a.Name?.Contains("Infrastructure") == true);
    }

    [Fact]
    public void Contracts_ShouldOnlyContainAllowedTypes()
    {
        var contractTypes = typeof(IIdentityPatientsModule).Assembly.GetTypes();

        foreach (var type in contractTypes)
        {
            // Contracts không được chứa DbContext hay Entity
            Assert.DoesNotContain("DbContext", type.Name);
            Assert.DoesNotContain("Entity", type.Name);
        }
    }
}

