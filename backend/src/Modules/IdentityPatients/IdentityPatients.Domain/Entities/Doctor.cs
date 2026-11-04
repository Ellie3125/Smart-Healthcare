namespace IdentityPatients.Domain.Entities;

public sealed class Doctor
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public Guid SpecialtyId { get; set; }
    public Guid DoctorRankId { get; set; }
    public Guid? DefaultRoomId { get; set; }
    public Guid? StaffId { get; set; }
    public string? Biography { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties trong cùng schema identity
    public Specialty Specialty { get; set; } = null!;
    public DoctorRank DoctorRank { get; set; } = null!;
}
