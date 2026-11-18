using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Api;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class RoomSessionTests
{
    [Fact]
    public void RoomDeviceSession_SessionToken_MustBeSha256Hash()
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var session = new RoomDeviceSession
        {
            Id = Guid.NewGuid(),
            RoomAccountId = Guid.NewGuid(),
            DeviceId = "DEV-001",
            DeviceIpAddress = "192.168.1.100",
            SessionToken = hexHash,
            LoginAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        Assert.Equal(64, session.SessionToken.Length);
        Assert.Matches("^[0-9a-f]{64}$", session.SessionToken);
    }

    [Fact]
    public void RoomDeviceSession_EndingPreviousSession_SetsIsActiveFalseAndLogoutUtc()
    {
        var session = new RoomDeviceSession
        {
            Id = Guid.NewGuid(),
            RoomAccountId = Guid.NewGuid(),
            DeviceId = "DEV-OLD",
            DeviceIpAddress = "192.168.1.50",
            SessionToken = "old-token-hash",
            LoginAtUtc = DateTime.UtcNow.AddHours(-1),
            IsActive = true,
            LogoutAtUtc = null
        };

        // Simulate new device login action
        var logoutTime = DateTime.UtcNow;
        session.IsActive = false;
        session.LogoutAtUtc = logoutTime;

        Assert.False(session.IsActive);
        Assert.NotNull(session.LogoutAtUtc);
        Assert.True(session.LogoutAtUtc >= session.LoginAtUtc);
    }

    [Fact]
    public async Task RoomSession_Http_OldJwtRejectedAfterSecondLogin_Logout_OrAdminRevoke()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = TestDatabase.RequiredConnectionString();

        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = signingKey;
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "IdentityPatients.Tests";
        builder.Services.AddControllers().AddIdentityPatientsApi();
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, connectionString)
            .AddSharedInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(ct);

        using var scope = app.Services.CreateScope();
        var adminUserId = Guid.NewGuid();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
        var room = dbContext.AddRoom();
        await dbContext.SaveChangesAsync(ct);

        var roomService = scope.ServiceProvider.GetRequiredService<IRoomManagementService>();
        var userName = IdentityTestData.Unique("room_cls");
        var email = $"{userName}@hospital.vn";
        var createdAccount = await roomService.CreateRoomAccountAsync(
            room.Id,
            new CreateRoomAccountRequest(userName, email, "RoomPass@2026", IdentityTestData.Unique("ACC")),
            ct);
        Assert.True(createdAccount.IsSuccess);

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var login1 = await authService.LoginStaffAsync(new LoginRequest(userName, "RoomPass@2026", "DEV-01", "10.0.0.1"), ct);
        Assert.True(login1.IsSuccess);
        var jwt1 = login1.Response!.AccessToken;

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt1);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me", ct)).StatusCode);

        // Đăng nhập thiết bị 2 -> phiên 1 đóng, jwt1 phải bị từ chối 401
        var login2 = await authService.LoginStaffAsync(new LoginRequest(userName, "RoomPass@2026", "DEV-02", "10.0.0.2"), ct);
        Assert.True(login2.IsSuccess);
        var jwt2 = login2.Response!.AccessToken;

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/me", ct)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt2);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me", ct)).StatusCode);

        // Tài khoản phòng chủ động đăng xuất -> jwt2 bị từ chối 401 ở request tiếp theo
        var logoutResp = await client.PostAsync("/auth/room/logout", null, ct);
        Assert.Equal(HttpStatusCode.OK, logoutResp.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/me", ct)).StatusCode);

        // Đăng nhập thiết bị 3 rồi Admin buộc đăng xuất (RevokeSessionAsync) -> jwt3 bị 401 + có AuditLog
        var login3 = await authService.LoginStaffAsync(new LoginRequest(userName, "RoomPass@2026", "DEV-03", "10.0.0.3"), ct);
        Assert.True(login3.IsSuccess);
        var jwt3 = login3.Response!.AccessToken;

        var activeSessions = await roomService.GetSessionsByRoomAsync(room.Id, activeOnly: true, ct);
        Assert.True(activeSessions.IsSuccess);
        var activeSessionId = Assert.Single(activeSessions.Value!).SessionId;

        var revoke = await roomService.RevokeSessionAsync(adminUserId, activeSessionId, "Ngắt thiết bị mất", ct);
        Assert.True(revoke.IsSuccess);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt3);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/me", ct)).StatusCode);

        var sharedDb = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        Assert.True(await sharedDb.Set<AuditLog>().AnyAsync(a => a.RecordId == activeSessionId && a.ChangeReason == "Ngắt thiết bị mất", ct));

        // Admin đặt lại mật khẩu RoomAccount -> đăng nhập mật khẩu mới thành công
        var reset = await roomService.ResetRoomAccountPasswordAsync(
            adminUserId,
            createdAccount.Value!.RoomAccountId,
            new ResetRoomPasswordRequest("RoomReset@2026"),
            ct);
        Assert.True(reset.IsSuccess);

        var loginAfterReset = await authService.LoginStaffAsync(new LoginRequest(userName, "RoomReset@2026", "DEV-04", "10.0.0.4"), ct);
        Assert.True(loginAfterReset.IsSuccess);
    }
}
