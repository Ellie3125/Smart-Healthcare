using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartHealthcare.Identity.Application;
using SmartHealthcare.Identity.Contracts;

namespace SmartHealthcare.Identity.Infrastructure.Security;

/// <summary>Cấu hình access token. SigningKey lấy từ user-secrets hoặc biến môi trường, không commit vào repo.</summary>
public sealed class JwtTokenOptions
{
    public const string Section = "Jwt";
    private const int MinKeyBytes = 32;

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 60;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>Dừng khởi động ngay nếu thiếu cấu hình, thay vì phát token không an toàn.</summary>
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Thiếu Jwt:Issuer hoặc Jwt:Audience.");
        if (Encoding.UTF8.GetByteCount(SigningKey) < MinKeyBytes)
            throw new InvalidOperationException($"Jwt:SigningKey cần ít nhất {MinKeyBytes} byte; đặt bằng user-secrets hoặc biến môi trường.");
        if (AccessTokenMinutes is < 5 or > 1440)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes phải từ 5 đến 1440.");
    }
}

internal sealed class JwtAccessTokenIssuer(JwtTokenOptions options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials = new(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);

    public IssuedToken Issue(Guid userId, string role)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [AuthenticatedUser.UserIdClaim] = userId.ToString(),
                [AuthenticatedUser.RoleClaim] = role,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        });
        return new IssuedToken(token, expires);
    }
}
