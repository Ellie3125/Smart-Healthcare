using Clinical.Application;
using Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinical.Infrastructure.Persistence.Stores;

internal sealed class EfPrescriptionStore(ClinicalDbContext db) : IPrescriptionStore
{
    public async Task<IReadOnlyList<Prescription>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<Prescription>()
            .AsNoTracking()
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.IssuedAtUtc)
            .ToListAsync(ct);

    public Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct) =>
        db.Set<Prescription>().FirstOrDefaultAsync(p => p.Id == prescriptionId, ct);

    public async Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct) =>
        await db.Set<PrescriptionItem>()
            .AsNoTracking()
            .Where(i => i.PrescriptionId == prescriptionId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PrescriptionItem>> GetItemsByPrescriptionIdsAsync(IReadOnlyCollection<Guid> prescriptionIds, CancellationToken ct)
    {
        if (prescriptionIds.Count == 0)
            return [];

        return await db.Set<PrescriptionItem>()
            .AsNoTracking()
            .Where(i => prescriptionIds.Contains(i.PrescriptionId))
            .ToListAsync(ct);
    }

    public void AddPrescription(Prescription prescription) => db.Set<Prescription>().Add(prescription);

    public void AddItem(PrescriptionItem item) => db.Set<PrescriptionItem>().Add(item);

    public void RemoveItem(PrescriptionItem item) => db.Set<PrescriptionItem>().Remove(item);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

