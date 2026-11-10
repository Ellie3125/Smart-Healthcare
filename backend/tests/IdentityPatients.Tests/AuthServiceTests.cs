using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Services;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AuthServiceTests
{
    private sealed class FakeTokenService : ITokenService
    {
        public string LastChannel { get; private set; } = string.Empty;
        public string LastRole { get; private set; } = string.Empty;

        public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null)
        {
            LastChannel = channel;
            LastRole = role;
            return "fake-jwt-token";
        }
    }

    private static async Task<(TestDatabase db, AuthService auth, FakeTokenService tokens, AppUser user)> CreateSystemAsync(string role, bool isActive = true)
    {
        var db = await TestDatabase.OpenAsync();
        var context = db.Context();
        var user = context.AddUser(role, isActive);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokens = new FakeTokenService();
        return (db, new AuthService(context, tokens), tokens, user);
    }

    [Fact]
    public async Task LoginStaff_WhenPasswordIsWrong_Returns401()
    {
        var (db, auth, _, user) = await CreateSystemAsync(Roles.Doctor);
        await using var _ = db;

        var result = await auth.LoginStaffAsync(new LoginRequest(user.UserName, "wrong-pass"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Đăng nhập thất bại", result.ErrorTitle);
    }

    [Fact]
    public async Task LoginPatient_WhenAccountIsInactive_Returns401EvenWithCorrectPassword()
    {
        var (db, auth, _, user) = await CreateSystemAsync(Roles.Patient, isActive: false);
        await using var _ = db;

        var result = await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task LoginStaff_WhenPatientAttemptsLogin_Returns403Forbidden()
    {
        var (db, auth, _, user) = await CreateSystemAsync(Roles.Patient);
        await using var _ = db;

        var result = await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Truy cập bị từ chối", result.ErrorTitle);
        Assert.Contains("kênh nhân viên", result.ErrorDetail);
    }

    [Fact]
    public async Task LoginPatient_WhenStaffAttemptsLogin_Returns403Forbidden()
    {
        var (db, auth, _, user) = await CreateSystemAsync(Roles.Doctor);
        await using var _ = db;

        var result = await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Truy cập bị từ chối", result.ErrorTitle);
        Assert.Contains("kênh bệnh nhân", result.ErrorDetail);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_Returns401WithoutRevealingUserExistence()
    {
        var (db, auth, _, _) = await CreateSystemAsync(Roles.Doctor);
        await using var _ = db;

        var resultStaff = await auth.LoginStaffAsync(new LoginRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "pass"), TestContext.Current.CancellationToken);
        var resultPatient = await auth.LoginPatientAsync(new LoginRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "pass"), TestContext.Current.CancellationToken);

        Assert.Equal(401, resultStaff.StatusCode);
        Assert.Equal("Đăng nhập thất bại", resultStaff.ErrorTitle);
        Assert.Equal(401, resultPatient.StatusCode);
        Assert.Equal("Đăng nhập thất bại", resultPatient.ErrorTitle);
    }

    [Fact]
    public async Task LoginStaff_WhenAuthorizedStaffLogsIn_ReturnsSuccessWithStaffChannelToken()
    {
        var (db, auth, tokens, user) = await CreateSystemAsync(Roles.Receptionist);
        await using var _ = db;

        var result = await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(Roles.Receptionist, result.Response.Role);
        Assert.Equal(Channels.Staff, tokens.LastChannel);
        Assert.Equal(Roles.Receptionist, tokens.LastRole);
    }

    [Fact]
    public async Task LoginPatient_WhenPatientLogsIn_ReturnsSuccessWithPatientChannelToken()
    {
        var (db, auth, tokens, user) = await CreateSystemAsync(Roles.Patient);
        await using var _ = db;

        var result = await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(Roles.Patient, result.Response.Role);
        Assert.Equal(Channels.Patient, tokens.LastChannel);
        Assert.Equal(Roles.Patient, tokens.LastRole);
    }
}
