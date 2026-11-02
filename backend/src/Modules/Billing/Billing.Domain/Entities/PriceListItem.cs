using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class PriceListItem
{
    public Guid Id { get; set; }
    public Guid PriceListId { get; set; }
    public ItemType ItemType { get; set; }
    // ID chéo module: identity.DoctorRank, không FK
    public Guid? DoctorRankId { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid? DoctorId { get; set; }
    // ID chéo module: scheduling.MedicalService, không FK
    public Guid? MedicalServiceId { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}
