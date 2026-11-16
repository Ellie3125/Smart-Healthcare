using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
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

    public async Task<IReadOnlyList<BillingItem>> GetClsByServiceOrderAsync(Guid serviceOrderId, CancellationToken ct) =>
        await db.Set<BillingItem>().AsNoTracking()
            .Where(b => b.ServiceOrderId == serviceOrderId && b.ItemType == ItemType.CLSService)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BillingItem>> GetClsByPatientAsync(Guid patientId, DateTime fromUtc, CancellationToken ct) =>
        await db.Set<BillingItem>().AsNoTracking()
            .Where(b => b.PatientId == patientId && b.ItemType == ItemType.CLSService && b.CreatedAtUtc >= fromUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindUnpaidClsBeforeAsync(DateTime beforeUtc, int take, CancellationToken ct) =>
        await db.Set<BillingItem>().AsNoTracking()
            .Where(b => b.ItemType == ItemType.CLSService && b.Status == BillingItemStatus.Pending && b.CreatedAtUtc < beforeUtc)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .Take(take)
            .ToListAsync(ct);

    public void Add(BillingItem item) => db.Set<BillingItem>().Add(item);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
