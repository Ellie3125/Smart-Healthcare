namespace SmartHealthcare.Identity.Domain;

public sealed class Patient
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? NationalId { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string Address { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
