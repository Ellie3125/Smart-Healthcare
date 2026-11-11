using Clinical.Application;
using Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinical.Infrastructure.Persistence.Stores;

internal sealed class EfServiceOrderStore(ClinicalDbContext db) : IServiceOrderStore
{
    public async Task<IReadOnlyList<ServiceOrder>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<ServiceOrder>()
            .AsNoTracking()
            .Where(o => o.EncounterId == encounterId)
            .OrderBy(o => o.OrderRoundNumber)
            .ToListAsync(ct);

    public Task<ServiceOrder?> GetByIdAsync(Guid orderId, CancellationToken ct) =>
        db.Set<ServiceOrder>().FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public Task<ServiceOrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct) =>
        db.Set<ServiceOrderItem>().FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public async Task<IReadOnlyList<ServiceOrderItem>> GetItemsByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        await db.Set<ServiceOrderItem>()
            .AsNoTracking()
            .Where(i => i.ServiceOrderId == orderId)
            .ToListAsync(ct);

    public Task<CLSResult?> GetResultByItemIdAsync(Guid itemId, CancellationToken ct) =>
        db.Set<CLSResult>().FirstOrDefaultAsync(r => r.ServiceOrderItemId == itemId, ct);

    public Task<CLSResult?> GetResultByIdAsync(Guid resultId, CancellationToken ct) =>
        db.Set<CLSResult>().FirstOrDefaultAsync(r => r.Id == resultId, ct);

    public async Task<IReadOnlyList<CLSResult>> GetResultsByOrderItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0)
            return [];

        return await db.Set<CLSResult>()
            .AsNoTracking()
            .Where(r => itemIds.Contains(r.ServiceOrderItemId))
            .ToListAsync(ct);
    }

    public void AddOrder(ServiceOrder order) => db.Set<ServiceOrder>().Add(order);

    public void AddOrderItem(ServiceOrderItem item) => db.Set<ServiceOrderItem>().Add(item);

    public void AddResult(CLSResult result) => db.Set<CLSResult>().Add(result);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

