namespace IdentityPatients.Domain.Entities;

public sealed class DoctorRank
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public int DisplayOrder { get => SortOrder; set => SortOrder = value; }
    public bool IsActive { get; set; } = true;
}
