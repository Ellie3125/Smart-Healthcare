namespace SmartHealthcare.Catalog.Domain;

public sealed class MedicationCatalog
{
    public Guid Id { get; set; }
    public string MedicationCode { get; set; } = null!;
    public string MedicationName { get; set; } = null!;
    public string ActiveIngredient { get; set; } = null!;
    public DosageForm DosageForm { get; set; }
    public string Strength { get; set; } = null!;
    public Unit Unit { get; set; }
    public bool IsActive { get; set; }
}
