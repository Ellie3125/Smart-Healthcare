namespace MedicationCatalog.Application;

public sealed record DrugCatalogItem(Guid Id, string Name, string ActiveIngredient, string Unit,
    string DefaultDosage, string Code, string Strength, string DosageForm);

public interface IDrugCatalog
{
    Task<IReadOnlyList<DrugCatalogItem>> SearchAsync(string query, CancellationToken ct);
}
