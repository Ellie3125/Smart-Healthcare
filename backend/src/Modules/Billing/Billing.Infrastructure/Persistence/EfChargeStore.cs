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

    public async Task<IReadOnlyList<Guid>> FindExamChargeIdsAsync(Guid bookingAttemptId, Guid? appointmentId, CancellationToken ct)
    {
        var paid = await (
                from intent in db.Set<PaymentIntent>()
                join transaction in db.Set<PaymentTransaction>() on intent.Id equals transaction.PaymentIntentId
                join allocation in db.Set<PaymentAllocation>() on transaction.Id equals allocation.PaymentTransactionId
                join item in db.Set<BillingItem>() on allocation.BillingItemId equals item.Id
                where intent.BookingAttemptId == bookingAttemptId && intent.Status == PaymentIntentStatus.Succeeded
                      && item.ItemType == ItemType.ConsultationFee
                select item.Id)
            .ToListAsync(ct);
        if (appointmentId is { } previous)
            paid.AddRange(await FindExamChargeIdsByAppointmentAsync(previous, ct));

        return paid.Distinct().ToList();
    }

    public async Task<IReadOnlyList<BillingItem>> GetClsSinceAsync(DateTime fromUtc, int take, CancellationToken ct) =>
        await db.Set<BillingItem>().AsNoTracking()
            .Where(b => b.ItemType == ItemType.CLSService && b.CreatedAtUtc >= fromUtc && b.ServiceOrderId != null)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindExamChargeIdsByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        await db.Set<BillingItem>()
            .Where(b => b.AppointmentId == appointmentId && b.ItemType == ItemType.ConsultationFee)
            .Select(b => b.Id)
            .ToListAsync(ct);

    public void Add(BillingItem item) => db.Set<BillingItem>().Add(item);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
