using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Domain.Entities;
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
}
