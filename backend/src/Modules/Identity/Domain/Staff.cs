namespace SmartHealthcare.Identity.Domain;

public sealed class Staff
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public string StaffCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public StaffType StaffType { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
