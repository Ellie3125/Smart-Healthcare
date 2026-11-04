using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Persistence;

/// <summary>
/// Alias theo yêu cầu thiết kế IdentityDbContext với Schema "identity"
/// </summary>
public class IdentityDbContext(DbContextOptions<IdentityPatientsDbContext> options) : IdentityPatientsDbContext(options)
{
}

