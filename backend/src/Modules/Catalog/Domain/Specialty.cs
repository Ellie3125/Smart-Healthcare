namespace SmartHealthcare.Catalog.Domain;

public sealed class Specialty
{
    public Guid Id { get; set; }
    public string SpecialtyCode { get; set; } = null!;
    public string SpecialtyName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
