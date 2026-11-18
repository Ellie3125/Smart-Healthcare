using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Queues;

public sealed class SqlClsServiceDirectory(SchedulingDbContext db) : IClsServiceDirectory
{
    public async Task<IReadOnlyList<OrderableClsService>> GetAsync(IReadOnlyList<Guid> serviceIds, CancellationToken ct)
    {
        if (serviceIds is not { Count: > 0 and <= 50 } || serviceIds.Contains(Guid.Empty) || serviceIds.Distinct().Count() != serviceIds.Count)
            throw new SchedulingException(SchedulingErrorCode.InvalidInput, "Danh sách dịch vụ không hợp lệ.");
        return await db.Set<MedicalService>().AsNoTracking().Where(x => serviceIds.Contains(x.Id) && x.IsActive)
            .OrderBy(x => x.Id).Select(x => new OrderableClsService(x.Id, x.ServiceName, x.IsMandatoryDefault)).ToListAsync(ct);
    }
}
