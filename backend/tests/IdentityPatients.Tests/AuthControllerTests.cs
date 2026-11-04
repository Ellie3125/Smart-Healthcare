using IdentityPatients.Api.Controllers;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AuthControllerTests : IDisposable
{
    private readonly IdentityPatientsDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher;
    private readonly TokenService _tokenService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new IdentityPatientsDbContext(options);
        _hasher = new PasswordHasher<AppUser>();
        
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "SmartHealthcareTest",
            Audience = "SmartHealthcareTest",
            SigningKey = "AuthControllerTestSecretKeyAtLeast32BytesLong!",
            AccessTokenMinutes = 60
        });
        _tokenService = new TokenService(jwtOptions);

        _controller = new AuthController(_db, _hasher, _tokenService);
    }

    [Fact]
    public async Task StaffLogin_with_Patient_role_returns_403_Forbidden()
    {
        var patientUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "patient01",
            Role = Roles.Patient,
            IsActive = true,
            RowVersion = new byte[8]
        };
        patientUser.PasswordHash = _hasher.HashPassword(patientUser, "Dev@12345");
        _db.AppUsers.Add(patientUser);
        await _db.SaveChangesAsync();

        var result = await _controller.StaffLogin(new LoginRequest("patient01", "Dev@12345"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Forbidden", problem.Title);
    }

    [Fact]
    public async Task PatientLogin_with_Staff_role_returns_403_Forbidden()
    {
        var doctorUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bacsi01",
            Role = Roles.Doctor,
            IsActive = true,
            RowVersion = new byte[8]
        };
        doctorUser.PasswordHash = _hasher.HashPassword(doctorUser, "Dev@12345");
        _db.AppUsers.Add(doctorUser);
        await _db.SaveChangesAsync();

        var result = await _controller.PatientLogin(new LoginRequest("bacsi01", "Dev@12345"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task StaffLogin_with_wrong_password_returns_401_Unauthorized()
    {
        var doctorUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bacsi01",
            Role = Roles.Doctor,
            IsActive = true,
            RowVersion = new byte[8]
        };
        doctorUser.PasswordHash = _hasher.HashPassword(doctorUser, "Dev@12345");
        _db.AppUsers.Add(doctorUser);
        await _db.SaveChangesAsync();

        var result = await _controller.StaffLogin(new LoginRequest("bacsi01", "WrongPassword!"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

