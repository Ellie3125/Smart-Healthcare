using System.Text.Json;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace Shared.Infrastructure.Tests;

public sealed class RoomAuditLogTests
{
    private static RoomAuditContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "hospital-device", Guid.NewGuid(), Guid.NewGuid(), new(2026, 11, 17), "Morning", Guid.NewGuid());

    [Fact]
    public void Captures_room_session_shift_and_operation_without_losing_changes()
    {
        var context = Context();
        var entry = new AuditLog { OldValuesJson = "{\"Status\":\"Called\"}", NewValuesJson = "{\"Status\":\"InProgress\"}" };
        Assert.Same(entry, entry.WithRoomContext(context));
        Assert.Equal(context.UserId, entry.PerformedByUserId);
        Assert.Equal(context.RoomAccountId, entry.RoomAccountId);
        Assert.Equal(context.SessionId, entry.RoomDeviceSessionId);
        Assert.Equal(context.DeviceId, entry.DeviceId);
        Assert.Equal(context.ShiftAssignmentId, entry.StaffShiftAssignmentId);
        Assert.Equal(context.OperationId, entry.OperationId);
        using var before = JsonDocument.Parse(entry.OldValuesJson!);
        using var after = JsonDocument.Parse(entry.NewValuesJson!);
        Assert.Equal("Called", before.RootElement.GetProperty("Values").GetProperty("Status").GetString());
        Assert.Equal("InProgress", after.RootElement.GetProperty("Values").GetProperty("Status").GetString());
        var metadata = after.RootElement.GetProperty("RoomContext");
        Assert.Equal(context.RoomId, metadata.GetProperty("RoomId").GetGuid());
        Assert.Equal("2026-11-17", metadata.GetProperty("ShiftDate").GetString());
        Assert.Equal(context.ShiftName, metadata.GetProperty("ShiftName").GetString());
        Assert.Equal(context.ShiftLeaderStaffId, metadata.GetProperty("ShiftLeaderStaffId").GetGuid());
    }

    [Fact]
    public void Creation_and_deletion_keep_null_snapshot_and_metadata()
    {
        var entry = new AuditLog().WithRoomContext(Context());
        using var before = JsonDocument.Parse(entry.OldValuesJson!);
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("Values").ValueKind);
        Assert.Equal(entry.OldValuesJson, entry.NewValuesJson);
    }

    [Theory]
    [InlineData("UserId")]
    [InlineData("RoomAccountId")]
    [InlineData("SessionId")]
    [InlineData("RoomId")]
    [InlineData("ShiftAssignmentId")]
    [InlineData("ShiftLeaderStaffId")]
    [InlineData("OperationId")]
    public void Rejects_missing_identifiers(string field)
    {
        var context = Context();
        typeof(RoomAuditContext).GetProperty(field)!.SetValue(context, Guid.Empty);
        Assert.Throws<ArgumentException>(() => new AuditLog().WithRoomContext(context));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Rejects_missing_device_or_shift(string? value)
    {
        Assert.Throws<ArgumentException>(() => new AuditLog().WithRoomContext(Context() with { DeviceId = value! }));
        Assert.Throws<ArgumentException>(() => new AuditLog().WithRoomContext(Context() with { ShiftName = value! }));
    }

    [Fact]
    public void Rejects_oversized_device_default_date_and_invalid_snapshots_without_partial_mutation()
    {
        Assert.Throws<ArgumentException>(() => new AuditLog().WithRoomContext(Context() with { DeviceId = new('a', 65) }));
        Assert.Throws<ArgumentException>(() => new AuditLog().WithRoomContext(Context() with { ShiftDate = default }));
        var entry = new AuditLog { OldValuesJson = "{}", NewValuesJson = "invalid json" };
        Assert.Throws<JsonException>(() => entry.WithRoomContext(Context()));
        Assert.Null(entry.RoomAccountId);
        Assert.Equal("{}", entry.OldValuesJson);
    }
}
