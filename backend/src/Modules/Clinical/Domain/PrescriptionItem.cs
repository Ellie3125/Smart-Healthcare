namespace SmartHealthcare.Clinical.Domain;

public sealed class PrescriptionItem
{
    public Guid Id { get; set; }
    public Guid PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;
    public Guid MedicationId { get; set; }
    public string MedicationNameSnapshot { get; set; } = null!;
    public string ActiveIngredientSnapshot { get; set; } = null!;
    public string Dosage { get; set; } = null!;
    public Route Route { get; set; }
    public string Frequency { get; set; } = null!;
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    public string? UsageInstructions { get; set; }
}
