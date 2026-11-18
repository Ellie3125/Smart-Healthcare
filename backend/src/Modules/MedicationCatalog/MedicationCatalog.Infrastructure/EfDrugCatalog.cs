using MedicationCatalog.Application;
using MedicationCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Medication = MedicationCatalog.Domain.Entities.MedicationCatalog;

namespace MedicationCatalog.Infrastructure;

internal sealed class EfDrugCatalog(MedicationDbContext db) : IDrugCatalog
{
    public async Task<IReadOnlyList<DrugCatalogItem>> SearchAsync(string query, CancellationToken ct) =>
        await db.Set<Medication>().AsNoTracking()
            .Where(m => m.IsActive && (query == "" || m.MedicationCode.StartsWith(query)
                || m.MedicationName.StartsWith(query) || m.ActiveIngredient.StartsWith(query)))
            .OrderBy(m => m.MedicationName).ThenBy(m => m.MedicationCode).ThenBy(m => m.Id).Take(20)
            .Select(m => new DrugCatalogItem(m.Id, m.MedicationName, m.ActiveIngredient, m.Unit.ToString(),
                "", m.MedicationCode, m.Strength, m.DosageForm.ToString())).ToListAsync(ct);
}
