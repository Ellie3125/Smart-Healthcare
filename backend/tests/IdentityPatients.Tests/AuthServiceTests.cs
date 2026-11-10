using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AuthServiceTests
{
    private sealed class FakeTokenService : ITokenService
    {
        public string LastChannel { get; private set; } = string.Empty;
        public string LastRole { get; private set; } = string.Empty;
        public Guid? LastRoomId { get; private set; }

        public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null)
        {
            LastChannel = channel;
            LastRole = role;
            LastRoomId = roomId;
            return "fake-jwt-token";
        }
    }

    private static async Task<(IdentityTestHost host, AuthService auth, FakeTokenService tokens, AppUser user)> CreateSystemAsync(string role, bool isActive = true)
    {
        var host = await IdentityTestHost.OpenAsync();
        var user = host.Context.AddUser(role, isActive);
        await host.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokens = new FakeTokenService();
        return (host, new AuthService(host.Context, tokens, host.Get<IUnitOfWork>(), host.Get<AuditLogWriter>()), tokens, user);
    }

    private static void AssertInvalidCredentials(AuthResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Đăng nhập thất bại", result.ErrorTitle);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không chính xác.", result.ErrorDetail);
        Assert.Equal(AuthService.InvalidCredentialsCode, result.ErrorCode);
    }

    [Fact]
    public async Task LoginStaff_WhenPasswordIsWrong_Returns401()
    {
        var (host, auth, _, user) = await CreateSystemAsync(Roles.Doctor);
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginStaffAsync(new LoginRequest(user.UserName, "wrong-pass"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoginPatient_WhenAccountIsInactive_Returns401EvenWithCorrectPassword()
    {
        var (host, auth, _, user) = await CreateSystemAsync(Roles.Patient, isActive: false);
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken));
    }

    // D8: sai kênh trả cùng 401 như sai mật khẩu, không cho biết tài khoản tồn tại ở kênh kia.
    [Fact]
    public async Task LoginStaff_WhenPatientAttemptsLogin_Returns401()
    {
        var (host, auth, _, user) = await CreateSystemAsync(Roles.Patient);
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.RoomAccount)]
    public async Task LoginPatient_WhenStaffAttemptsLogin_Returns401(string role)
    {
        var (host, auth, _, user) = await CreateSystemAsync(role);
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoginStaff_WhenTechnician_Returns401()
    {
        var (host, auth, _, user) = await CreateSystemAsync(nameof(StaffType.Technician));
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Login_WhenUserDoesNotExist_Returns401()
    {
        var (host, auth, _, _) = await CreateSystemAsync(Roles.Doctor);
        await using var _ = host;

        AssertInvalidCredentials(await auth.LoginStaffAsync(new LoginRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "pass"), TestContext.Current.CancellationToken));
        AssertInvalidCredentials(await auth.LoginPatientAsync(new LoginRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "pass"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Nurse)]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.RoomAccount)]
    public async Task LoginStaff_WhenStaffRoleLogsIn_ReturnsDerivedRoleWithStaffChannel(string role)
    {
        var (host, auth, tokens, user) = await CreateSystemAsync(role);
        await using var _ = host;

        var result = await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(role, result.Response!.Role);
        Assert.Equal(Channels.Staff, tokens.LastChannel);
        Assert.Equal(role, tokens.LastRole);
    }

    [Fact]
    public async Task LoginPatient_WhenPatientLogsIn_ReturnsSuccessWithPatientChannelToken()
    {
        var (host, auth, tokens, user) = await CreateSystemAsync(Roles.Patient);
        await using var _ = host;

        var result = await auth.LoginPatientAsync(new LoginRequest(user.UserName, IdentityTestData.Password), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(Roles.Patient, result.Response!.Role);
        Assert.Equal(Channels.Patient, tokens.LastChannel);
    }

    // Mỗi RoomAccount tối đa một phiên hoạt động: đăng nhập lần hai đóng phiên cũ và ghi AuditLog cùng unit of work.
    [Fact]
    public async Task LoginRoomAccount_Twice_KeepsOneActiveSessionAndAudits()
    {
        var (host, auth, tokens, user) = await CreateSystemAsync(Roles.RoomAccount);
        await using var _ = host;
        var ct = TestContext.Current.CancellationToken;
        var roomAccount = await host.Context.RoomAccounts.SingleAsync(r => r.AppUserId == user.Id, ct);

        await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password, "DEV-1"), ct);
        await auth.LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password, "DEV-2"), ct);

        var sessions = await host.Context.RoomDeviceSessions.AsNoTracking().Where(s => s.RoomAccountId == roomAccount.Id).ToListAsync(ct);
        Assert.Equal(2, sessions.Count);
        Assert.Equal("DEV-2", Assert.Single(sessions, s => s.IsActive).DeviceId);
        Assert.Equal(roomAccount.RoomId, tokens.LastRoomId);
        Assert.True(await host.Get<SharedDbContext>().Set<Shared.Infrastructure.Persistence.Entities.AuditLog>()
            .AnyAsync(a => a.RoomAccountId == roomAccount.Id && a.TableName == "identity.RoomDeviceSession", ct));
    }

    [Theory]
    [InlineData(true, null, Roles.RoomAccount)]
    [InlineData(true, StaffType.Doctor, Roles.RoomAccount)]
    [InlineData(false, StaffType.Doctor, Roles.Doctor)]
    [InlineData(false, StaffType.Nurse, Roles.Nurse)]
    [InlineData(false, StaffType.Receptionist, Roles.Receptionist)]
    [InlineData(false, StaffType.Admin, Roles.Admin)]
    [InlineData(false, StaffType.Technician, null)]
    [InlineData(false, null, Roles.Patient)]
    public void RoleFor_CoversEveryBranch(bool hasRoomAccount, StaffType? staffType, string? expected) =>
        Assert.Equal(expected, AccountRoles.RoleFor(hasRoomAccount, staffType));

    [Fact]
    public async Task Resolve_WhenStaffIsInactive_ReturnsNull()
    {
        var (host, _, _, user) = await CreateSystemAsync(Roles.Receptionist);
        await using var _ = host;
        var staff = await host.Context.Staffs.SingleAsync(s => s.AppUserId == user.Id, TestContext.Current.CancellationToken);
        staff.IsActive = false;
        await host.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(await AccountRoles.ResolveAsync(host.Context, user.Id, TestContext.Current.CancellationToken));
    }
}
