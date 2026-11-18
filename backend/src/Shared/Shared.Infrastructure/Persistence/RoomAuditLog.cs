using System.Text.Json;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure.Persistence;

/// <summary>Server-resolved room session and shift; never bind this context from a request body.</summary>
public sealed record RoomAuditContext(Guid UserId, Guid RoomAccountId, Guid SessionId, Guid RoomId,
    string DeviceId, Guid ShiftAssignmentId, Guid ShiftLeaderStaffId, DateOnly ShiftDate, string ShiftName,
    Guid OperationId);

public static class RoomAuditLog
{
    /// <summary>Enrich a change before passing it to AuditLogWriter in the business unit of work.</summary>
    public static AuditLog WithRoomContext(this AuditLog entry, RoomAuditContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);
        if (context.UserId == Guid.Empty || context.RoomAccountId == Guid.Empty || context.SessionId == Guid.Empty
            || context.RoomId == Guid.Empty || context.ShiftAssignmentId == Guid.Empty
            || context.ShiftLeaderStaffId == Guid.Empty || context.OperationId == Guid.Empty
            || context.ShiftDate == default || string.IsNullOrWhiteSpace(context.ShiftName)
            || string.IsNullOrWhiteSpace(context.DeviceId) || context.DeviceId.Length > 64)
            throw new ArgumentException("Room audit requires a complete, server-resolved session and shift.", nameof(context));

        var metadata = new { context.RoomId, context.ShiftDate, context.ShiftName, context.ShiftLeaderStaffId };
        // Parse both snapshots first so invalid JSON leaves the caller's entry unchanged.
        var before = JsonSerializer.Serialize(new { RoomContext = metadata, Values = Snapshot(entry.OldValuesJson) });
        var after = JsonSerializer.Serialize(new { RoomContext = metadata, Values = Snapshot(entry.NewValuesJson) });
        entry.PerformedByUserId = context.UserId;
        entry.RoomAccountId = context.RoomAccountId;
        entry.RoomDeviceSessionId = context.SessionId;
        entry.DeviceId = context.DeviceId;
        entry.StaffShiftAssignmentId = context.ShiftAssignmentId;
        entry.OperationId = context.OperationId;
        entry.OldValuesJson = before;
        entry.NewValuesJson = after;
        return entry;
    }

    private static JsonElement? Snapshot(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
