using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AuthServiceTests
{
    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUser? UserToReturn { get; set; }
        public IList<string> RolesToReturn { get; set; } = new List<string>();

        public FakeUserManager()
            : base(
                new MockUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                new LoggerFactory().CreateLogger<UserManager<ApplicationUser>>())
        {
        }

        public override Task<ApplicationUser?> FindByNameAsync(string userName)
        {
            if (UserToReturn != null && string.Equals(UserToReturn.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ApplicationUser?>(UserToReturn);
            }
            return Task.FromResult<ApplicationUser?>(null);
        }

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            return Task.FromResult(RolesToReturn);
        }
    }

    private sealed class FakeSignInManager : SignInManager<ApplicationUser>
    {
        public SignInResult ResultToReturn { get; set; } = SignInResult.Success;

        public FakeSignInManager(FakeUserManager userManager)
            : base(
                userManager,
                new HttpContextAccessor(),
                new MockUserClaimsPrincipalFactory(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new LoggerFactory().CreateLogger<SignInManager<ApplicationUser>>(),
                new MockAuthenticationSchemeProvider(),
                new DefaultUserConfirmation<ApplicationUser>())
        {
        }

        public override Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class MockUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
    }

    private sealed class MockUserClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<System.Security.Claims.ClaimsPrincipal> CreateAsync(ApplicationUser user) =>
            Task.FromResult(new System.Security.Claims.ClaimsPrincipal());
    }

    private sealed class MockAuthenticationSchemeProvider : IAuthenticationSchemeProvider
    {
        public Task<AuthenticationScheme?> GetDefaultAuthenticateSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultChallengeSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultForbidSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultSignInSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultSignOutSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetSchemeAsync(string name) => Task.FromResult<AuthenticationScheme?>(null);
        public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync() => Task.FromResult(Enumerable.Empty<AuthenticationScheme>());
        public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() => Task.FromResult(Enumerable.Empty<AuthenticationScheme>());
        public void AddScheme(AuthenticationScheme scheme) { }
        public void RemoveScheme(string name) { }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string LastChannel { get; private set; } = string.Empty;
        public string LastRole { get; private set; } = string.Empty;

        public string GenerateAccessToken(ApplicationUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null)
        {
            LastChannel = channel;
            LastRole = role;
            return "fake-jwt-token";
        }
    }

    private static (FakeUserManager userManager, FakeSignInManager signInManager, FakeTokenService tokenService, IdentityPatientsDbContext dbContext, AuthService authService) CreateSystem()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseSqlServer("Server=fake;Database=fake;")
            .Options;

        // Stub DbContext without real DB connection for pure unit test
        var dbContext = new IdentityPatientsDbContext(options);
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var tokenService = new FakeTokenService();
        var authService = new AuthService(userManager, signInManager, dbContext, tokenService);

        return (userManager, signInManager, tokenService, dbContext, authService);
    }

    [Fact]
    public async Task LoginStaff_WhenAccountIsLockedOut_Returns423ProblemDetails()
    {
        var (userManager, signInManager, _, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "doctor01", IsActive = true };
        userManager.UserToReturn = user;
        signInManager.ResultToReturn = SignInResult.LockedOut;

        var result = await authService.LoginStaffAsync(new LoginRequest("doctor01", "wrong-pass"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(423, result.StatusCode);
        Assert.Equal("Tài khoản bị tạm khóa", result.ErrorTitle);
        Assert.Contains("tạm khóa", result.ErrorDetail);
    }

    [Fact]
    public async Task LoginPatient_WhenAccountIsLockedOut_Returns423ProblemDetails()
    {
        var (userManager, signInManager, _, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "patient01", IsActive = true };
        userManager.UserToReturn = user;
        signInManager.ResultToReturn = SignInResult.LockedOut;

        var result = await authService.LoginPatientAsync(new LoginRequest("patient01", "wrong-pass"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(423, result.StatusCode);
        Assert.Equal("Tài khoản bị tạm khóa", result.ErrorTitle);
        Assert.Contains("tạm khóa", result.ErrorDetail);
    }

    [Fact]
    public async Task LoginStaff_WhenPatientAttemptsLogin_Returns403Forbidden()
    {
        var (userManager, signInManager, _, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "patient01", IsActive = true };
        userManager.UserToReturn = user;
        userManager.RolesToReturn = [Roles.Patient];
        signInManager.ResultToReturn = SignInResult.Success;

        var result = await authService.LoginStaffAsync(new LoginRequest("patient01", "correct-pass"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Truy cập bị từ chối", result.ErrorTitle);
        Assert.Contains("kênh nhân viên", result.ErrorDetail);
    }

    [Fact]
    public async Task LoginPatient_WhenStaffAttemptsLogin_Returns403Forbidden()
    {
        var (userManager, signInManager, _, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "doctor01", IsActive = true };
        userManager.UserToReturn = user;
        userManager.RolesToReturn = [Roles.Doctor];
        signInManager.ResultToReturn = SignInResult.Success;

        var result = await authService.LoginPatientAsync(new LoginRequest("doctor01", "correct-pass"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Truy cập bị từ chối", result.ErrorTitle);
        Assert.Contains("kênh bệnh nhân", result.ErrorDetail);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_Returns401WithoutRevealingUserExistence()
    {
        var (userManager, signInManager, _, _, authService) = CreateSystem();
        userManager.UserToReturn = null; // User does not exist

        var resultStaff = await authService.LoginStaffAsync(new LoginRequest("nonexistent", "pass"), TestContext.Current.CancellationToken);
        var resultPatient = await authService.LoginPatientAsync(new LoginRequest("nonexistent", "pass"), TestContext.Current.CancellationToken);

        Assert.Equal(401, resultStaff.StatusCode);
        Assert.Equal("Đăng nhập thất bại", resultStaff.ErrorTitle);
        Assert.Equal(401, resultPatient.StatusCode);
        Assert.Equal("Đăng nhập thất bại", resultPatient.ErrorTitle);
    }

    [Fact]
    public async Task LoginStaff_WhenAuthorizedStaffLogsIn_ReturnsSuccessWithStaffChannelToken()
    {
        var (userManager, signInManager, tokenService, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "letan01", IsActive = true };
        userManager.UserToReturn = user;
        userManager.RolesToReturn = [Roles.Receptionist];
        signInManager.ResultToReturn = SignInResult.Success;

        var result = await authService.LoginStaffAsync(new LoginRequest("letan01", "correct-pass"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(Roles.Receptionist, result.Response.Role);
        Assert.Equal(Channels.Staff, tokenService.LastChannel);
        Assert.Equal(Roles.Receptionist, tokenService.LastRole);
    }

    [Fact]
    public async Task LoginPatient_WhenPatientLogsIn_ReturnsSuccessWithPatientChannelToken()
    {
        var (userManager, signInManager, tokenService, _, authService) = CreateSystem();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "patient01", IsActive = true };
        userManager.UserToReturn = user;
        userManager.RolesToReturn = [Roles.Patient];
        signInManager.ResultToReturn = SignInResult.Success;

        var result = await authService.LoginPatientAsync(new LoginRequest("patient01", "correct-pass"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(Roles.Patient, result.Response.Role);
        Assert.Equal(Channels.Patient, tokenService.LastChannel);
        Assert.Equal(Roles.Patient, tokenService.LastRole);
    }
}
