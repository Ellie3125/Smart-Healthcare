using Xunit;
using Contract = Notifications.Contracts;
using Domain = Notifications.Domain.Enums;

namespace ArchitectureTests;

// W2-03: EfOperationTracker ánh xạ enum Contracts ↔ Domain theo tên, nên hai bên phải cùng tập tên.
public class OperationEnumParityTests
{
    [Theory]
    [InlineData(typeof(Contract.OperationType), typeof(Domain.OperationType))]
    [InlineData(typeof(Contract.OperationStatus), typeof(Domain.OperationStatus))]
    [InlineData(typeof(Contract.TargetEntityType), typeof(Domain.TargetEntityType))]
    public void Contract_and_domain_enums_have_the_same_names(Type contract, Type domain) =>
        Assert.Equal(Enum.GetNames(domain).Order(), Enum.GetNames(contract).Order());
}
