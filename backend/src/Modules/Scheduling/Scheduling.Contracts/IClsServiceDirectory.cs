namespace Scheduling.Contracts;

public sealed record OrderableClsService(Guid Id, string Name, bool IsMandatory);

/// <summary>Clinical reads server-owned service names/defaults before ordering; no writes, no transaction required.</summary>
public interface IClsServiceDirectory
{
    /// <summary>Returns active services among 1–50 distinct nonempty IDs; missing/inactive IDs are omitted.</summary>
    Task<IReadOnlyList<OrderableClsService>> GetAsync(IReadOnlyList<Guid> serviceIds, CancellationToken ct);
}
