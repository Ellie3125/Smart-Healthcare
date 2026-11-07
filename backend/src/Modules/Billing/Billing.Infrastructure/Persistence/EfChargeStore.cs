using Billing.Application;
using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfChargeStore(BillingDbContext db) : IChargeStore
{
    public async Task<IReadOnlyList<BillingItem>> GetByServiceOrderItemsAsync(IReadOnlyCollection<Guid> serviceOrderItemIds, CancellationToken ct)
    {
        var ids = serviceOrderItemIds.ToList();
        return await db.Set<BillingItem>()
            .Where(b => b.ServiceOrderItemId != null && ids.Contains(b.ServiceOrderItemId.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BillingItem>> GetByEncounterAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<BillingItem>().AsNoTracking().Where(b => b.EncounterId == encounterId).ToListAsync(ct);

    public void Add(BillingItem item) => db.Set<BillingItem>().Add(item);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
