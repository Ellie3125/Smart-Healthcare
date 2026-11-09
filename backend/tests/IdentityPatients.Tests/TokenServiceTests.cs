using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = "A_Very_Secret_Key_For_Testing_Only_32_Bytes_Long!",
            AccessTokenMinutes = 30
        });

        _tokenService = new TokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_ForDoctor_ContainsDoctorIdClaimAndStaffChannel()
    {
        var doctorId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "dr.smith",
            IsActive = true
        };

        var tokenStr = _tokenService.GenerateAccessToken(user, Roles.Doctor, Channels.Staff, doctorId: doctorId);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(Roles.Doctor, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(Channels.Staff, jwt.Claims.First(c => c.Type == AppClaims.Channel).Value);
        Assert.Equal(doctorId.ToString(), jwt.Claims.First(c => c.Type == AppClaims.DoctorId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AppClaims.RoomId);
    }

    [Fact]
    public void GenerateAccessToken_ForRoomAccount_ContainsRoomIdClaimAndStaffChannel()
    {
        var roomId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "room_xq",
            IsActive = true
        };

        var tokenStr = _tokenService.GenerateAccessToken(user, Roles.RoomAccount, Channels.Staff, roomId: roomId);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(Roles.RoomAccount, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(Channels.Staff, jwt.Claims.First(c => c.Type == AppClaims.Channel).Value);
        Assert.Equal(roomId.ToString(), jwt.Claims.First(c => c.Type == AppClaims.RoomId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AppClaims.DoctorId);
    }

    [Fact]
    public void GenerateAccessToken_ForPatient_ContainsPatientChannel()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "patient.smith",
            IsActive = true
        };

        var tokenStr = _tokenService.GenerateAccessToken(user, Roles.Patient, Channels.Patient);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(Roles.Patient, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(Channels.Patient, jwt.Claims.First(c => c.Type == AppClaims.Channel).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AppClaims.DoctorId);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AppClaims.RoomId);
    }
}
