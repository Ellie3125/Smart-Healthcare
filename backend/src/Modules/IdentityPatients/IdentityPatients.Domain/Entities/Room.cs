namespace IdentityPatients.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string RoomNumber { get => Code; set => Code = value; }
    public string Name { get; set; } = null!;
    public string RoomName { get => Name; set => Name = value; }
    public string RoomType { get; set; } = null!;
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; } = true;
}
