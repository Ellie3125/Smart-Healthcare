using Microsoft.AspNetCore.Identity;
using SmartHealthcare.Identity.Application;

namespace SmartHealthcare.Identity.Infrastructure.Security;

/// <summary>PBKDF2 của ASP.NET Core Identity (định dạng V3). Không dùng tham số người dùng nên truyền một đối tượng cố định.</summary>
internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly object User = new();
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly Lazy<string> DummyHash = new(() => Hasher.HashPassword(User, Guid.NewGuid().ToString()));

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(User, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };

    public void VerifyAgainstDummy(string password) => Hasher.VerifyHashedPassword(User, DummyHash.Value, password);
}
