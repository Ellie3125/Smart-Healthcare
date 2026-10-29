namespace SmartHealthcare.Clinical.Domain;

public sealed class ServiceOrderItem
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;
    public Guid MedicalServiceId { get; set; }
    public string ServiceNameSnapshot { get; set; } = null!;
    public decimal AppliedPrice { get; set; }
    public ItemStatus ItemStatus { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
