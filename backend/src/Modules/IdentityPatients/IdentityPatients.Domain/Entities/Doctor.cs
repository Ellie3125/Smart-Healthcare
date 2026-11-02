namespace IdentityPatients.Domain.Entities;

public sealed class Doctor
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid SpecialtyId { get; set; }
    public Guid DoctorRankId { get; set; }
    public string? Biography { get; set; }
    public bool IsActive { get; set; }
}
