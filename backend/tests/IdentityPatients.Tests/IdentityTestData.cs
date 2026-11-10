using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using SqlServerTests;

namespace IdentityPatients.Tests;

// Dữ liệu test ghi vào SmartHealthcare_Test trong transaction của TestDatabase, rollback khi dispose (DB-06, DB-08).
internal static class IdentityTestData
{
    public const string Password = "test-password-123";

    // Hash một lần cho cả lớp test: PBKDF2 600.000 vòng tốn vài trăm ms mỗi lần.
    private static readonly Lazy<string> PasswordHash = new(() => PasswordHashing.Hash(Password));

    public static IdentityPatientsDbContext Context(this TestDatabase db) =>
        db.CreateContext<IdentityPatientsDbContext>(options => new(options));

    public static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 64)];

    public static AppUser AddUser(this IdentityPatientsDbContext context, string role, bool isActive = true)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = Unique("u"),
            PasswordHash = PasswordHash.Value,
            Role = role,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.AppUsers.Add(user);
        return user;
    }

    public static Patient AddPatient(this IdentityPatientsDbContext context, string? nationalId = null)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Bệnh nhân test",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = IdentityPatients.Domain.Enums.Gender.Male,
            NationalId = nationalId,
            PhoneNumber = "0900000000",
            Address = "Địa chỉ test",
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Patients.Add(patient);
        return patient;
    }
}
