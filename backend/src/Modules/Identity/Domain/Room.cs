namespace SmartHealthcare.Identity.Domain;

public sealed class Room
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = null!;
    public string RoomName { get; set; } = null!;
    public RoomType RoomType { get; set; }
    public string DepartmentName { get; set; } = null!;
    public bool IsActive { get; set; }
}
