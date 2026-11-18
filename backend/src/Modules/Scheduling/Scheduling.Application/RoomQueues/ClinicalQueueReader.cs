using Scheduling.Contracts;

namespace Scheduling.Application.RoomQueues;

public interface IClinicalQueueReader
{
    Task<ClinicalQueuePage> ReadAsync(Guid roomId, DateTime atUtc, int page, int pageSize, CancellationToken ct);
}
