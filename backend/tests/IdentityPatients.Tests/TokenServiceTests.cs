using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private const string SecretKey = "TestSecretKeyThatIsAtLeast32BytesLong!";

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "SmartHealthcareTest",
            Audience = "SmartHealthcareTest",
            SigningKey = SecretKey,
            AccessTokenMinutes = 60
        });

        _tokenService = new TokenService(options);
    }

    [Fact]
    public void Doctor_token_contains_doctor_id_claim()
    {
        var doctorId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bacsi_test",
            Role = Roles.Doctor,
            DoctorId = doctorId,
            IsActive = true
        };

        var tokenString = _tokenService.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        var doctorClaim = jwt.Claims.FirstOrDefault(c => c.Type == AppClaims.DoctorId)?.Value;
        Assert.NotNull(doctorClaim);
        Assert.Equal(doctorId.ToString(), doctorClaim);

        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        Assert.Equal(Roles.Doctor, roleClaim);
    }

    [Fact]
    public void RoomAccount_token_contains_room_id_claim()
    {
        var roomId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "phong_xq_test",
            Role = Roles.RoomAccount,
            RoomId = roomId,
            IsActive = true
        };

        var tokenString = _tokenService.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        var roomClaim = jwt.Claims.FirstOrDefault(c => c.Type == AppClaims.RoomId)?.Value;
        Assert.NotNull(roomClaim);
        Assert.Equal(roomId.ToString(), roomClaim);
    }

    [Fact]
    public void Patient_token_does_not_contain_doctor_or_room_claims()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "patient_test",
            Role = Roles.Patient,
            DoctorId = null,
            RoomId = null,
            IsActive = true
        };

        var tokenString = _tokenService.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == AppClaims.DoctorId));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == AppClaims.RoomId));
    }
}

