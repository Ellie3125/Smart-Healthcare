using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPatients.Infrastructure.Services;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null)
    {
        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < JwtOptions.MinSigningKeyBytes)
        {
            throw new InvalidOperationException("Jwt:SigningKey chưa được cấu hình hoặc ngắn hơn 32 byte.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Role, role),
            new(AppClaims.Channel, channel),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (doctorId.HasValue)
        {
            claims.Add(new Claim(AppClaims.DoctorId, doctorId.Value.ToString()));
        }

        if (roomId.HasValue)
        {
            claims.Add(new Claim(AppClaims.RoomId, roomId.Value.ToString()));
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

