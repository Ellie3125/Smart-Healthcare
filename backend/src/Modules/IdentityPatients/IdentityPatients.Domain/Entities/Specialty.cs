namespace IdentityPatients.Domain.Entities;

public sealed class Specialty
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string SpecialtyCode { get => Code; set => Code = value; }
    public string Name { get; set; } = null!;
    public string SpecialtyName { get => Name; set => Name = value; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
