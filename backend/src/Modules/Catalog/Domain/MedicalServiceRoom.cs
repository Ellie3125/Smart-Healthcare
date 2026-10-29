namespace SmartHealthcare.Catalog.Domain;

public sealed class MedicalServiceRoom
{
    public Guid Id { get; set; }
    public Guid MedicalServiceId { get; set; }
    public MedicalService MedicalService { get; set; } = null!;
    public Guid RoomId { get; set; }
    public bool IsDefaultRoom { get; set; }
    public bool IsActive { get; set; }
}
