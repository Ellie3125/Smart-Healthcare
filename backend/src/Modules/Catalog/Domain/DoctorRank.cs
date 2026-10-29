namespace SmartHealthcare.Catalog.Domain;

public sealed class DoctorRank
{
    public Guid Id { get; set; }
    public RankCode RankCode { get; set; }
    public string RankName { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
