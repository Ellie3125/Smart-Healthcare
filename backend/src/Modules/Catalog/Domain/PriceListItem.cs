namespace SmartHealthcare.Catalog.Domain;

public sealed class PriceListItem
{
    public Guid Id { get; set; }
    public Guid PriceListId { get; set; }
    public PriceList PriceList { get; set; } = null!;
    public ItemType ItemType { get; set; }
    public Guid? DoctorRankId { get; set; }
    public DoctorRank? DoctorRank { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? MedicalServiceId { get; set; }
    public MedicalService? MedicalService { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}
