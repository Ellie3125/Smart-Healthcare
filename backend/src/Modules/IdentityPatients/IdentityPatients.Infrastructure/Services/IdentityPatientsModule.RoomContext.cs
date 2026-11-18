using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public async Task<RoomOperationContext?> GetRoomOperationContextAsync(Guid appUserId, Guid roomId, string roomSessionToken,
        DateTime atUtc, CancellationToken ct = default)
    {
        if (_dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("Room context requires Identity in the caller's IUnitOfWork.");
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Room context requires UTC DB time.", nameof(atUtc));
        if (appUserId == Guid.Empty || roomId == Guid.Empty || string.IsNullOrWhiteSpace(roomSessionToken) || roomSessionToken.Length > 512) return null;
        var hash = RoomSessionTokenValidator.HashSessionToken(roomSessionToken);
        var day = DateOnly.FromDateTime(atUtc.AddHours(7));
        var rows = await _dbContext.Database.SqlQuery<RoomOperationContext>($"""
            SELECT TOP(2) a.AppUserId, a.Id AS RoomAccountId, d.Id AS RoomDeviceSessionId, a.RoomId,
                d.DeviceId, s.Id AS StaffShiftAssignmentId, s.StaffId AS ShiftLeaderStaffId, s.ShiftDate, s.ShiftName
            FROM [identity].[RoomAccount] a WITH (HOLDLOCK)
            JOIN [identity].[AppUser] u WITH (HOLDLOCK) ON u.Id=a.AppUserId
            JOIN [identity].[Room] r WITH (HOLDLOCK) ON r.Id=a.RoomId
            JOIN [identity].[RoomDeviceSession] d WITH (HOLDLOCK) ON d.RoomAccountId=a.Id
            JOIN [identity].[StaffShiftAssignment] s WITH (HOLDLOCK) ON s.RoomId=a.RoomId
            JOIN [identity].[Staff] leader WITH (HOLDLOCK) ON leader.Id=s.StaffId
            JOIN [identity].[AppUser] leaderUser WITH (HOLDLOCK) ON leaderUser.Id=leader.AppUserId
            WHERE a.AppUserId={appUserId} AND a.RoomId={roomId} AND a.IsActive=1 AND u.IsActive=1 AND r.IsActive=1
                AND d.IsActive=1 AND d.SessionToken={hash} AND leader.IsActive=1 AND leaderUser.IsActive=1
                AND s.IsShiftLeader=1 AND s.ShiftDate={day} AND s.StartsAtUtc<={atUtc} AND s.EndsAtUtc>{atUtc}
            ORDER BY s.Id
            """).ToListAsync(ct);
        return rows.Count == 1 ? rows[0] : null;
    }
}
