using System.Reflection;
using IdentityPatients.Contracts;
using Scheduling.Application.Queues;
using Xunit;

namespace Scheduling.Tests;

public sealed class QueueAvailabilityTests
{
    [Fact]
    public async Task Inactive_identity_room_is_rejected_before_resolving_or_creating_a_station()
    {
        var identity = DispatchProxy.Create<IIdentityPatientsModule, InactiveRoom>();
        var queue = new QueueService(null!, null!, null!, identity, null!, QueuePolicy.Default);
        var result = await queue.CallNextAsync(new(Guid.NewGuid(), null, Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(QueueResult.StationNotFound, result.Result);
    }
    public class InactiveRoom : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IIdentityPatientsModule.GetRoomAsync)
            ? Task.FromResult<RoomDto?>(new((Guid)args![0]!, "TEST", "Test", "Clinical", false)) : throw new NotSupportedException(method?.Name);
    }
}
