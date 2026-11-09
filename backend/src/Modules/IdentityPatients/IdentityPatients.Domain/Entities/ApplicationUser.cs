using Microsoft.AspNetCore.Identity;

namespace IdentityPatients.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
