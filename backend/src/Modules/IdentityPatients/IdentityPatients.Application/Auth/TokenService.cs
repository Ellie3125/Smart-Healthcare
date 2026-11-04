using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Contracts.Dtos;
using IdentityPatients.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPatients.Application.Auth;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey chưa được cấu hình hoặc ngắn hơn 32 ký tự.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.DoctorId.HasValue)
        {
            claims.Add(new Claim(AppClaims.DoctorId, user.DoctorId.Value.ToString()));
        }

        if (user.RoomId.HasValue)
        {
            claims.Add(new Claim(AppClaims.RoomId, user.RoomId.Value.ToString()));
        }

        var now = DateTime.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = now,
            Expires = now.AddMinutes(_options.AccessTokenMinutes),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public CurrentUserDto GetCurrentUserFromClaims(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var id = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;
        var userName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        Guid? doctorId = null;
        var doctorClaim = principal.FindFirstValue(AppClaims.DoctorId);
        if (Guid.TryParse(doctorClaim, out var parsedDoctorId))
            doctorId = parsedDoctorId;

        Guid? roomId = null;
        var roomClaim = principal.FindFirstValue(AppClaims.RoomId);
        if (Guid.TryParse(roomClaim, out var parsedRoomId))
            roomId = parsedRoomId;

        return new CurrentUserDto(id, userName, role, doctorId, roomId);
    }
}

