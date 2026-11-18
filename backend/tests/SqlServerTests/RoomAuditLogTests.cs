using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class RoomAuditLogTests
{
    [Fact]
    public async Task Persists_all_room_context_and_masks_nested_snapshots_in_the_same_transaction()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var db = database.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        var context = new RoomAuditContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "registered-device", Guid.NewGuid(), Guid.NewGuid(), new(2026, 11, 17), "Morning", Guid.NewGuid());
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(), TableName = "scheduling.QueueTicket", RecordId = Guid.NewGuid(),
            ActionType = ActionType.UPDATE, ChangedColumns = "QueueStatus", TimestampUtc = DateTime.UtcNow,
            OldValuesJson = """{"QueueStatus":"Called","PhoneNumber":"0912345678"}""",
            NewValuesJson = """{"QueueStatus":"InProgress","NationalId":"001203004567"}""",
        }.WithRoomContext(context);
        await new AuditLogWriter(db).WriteAsync(entry, TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var saved = await db.Set<AuditLog>().SingleAsync(x => x.Id == entry.Id, TestContext.Current.CancellationToken);
        Assert.Equal(context.RoomAccountId, saved.RoomAccountId);
        Assert.Equal(context.UserId, saved.PerformedByUserId);
        Assert.Equal(context.SessionId, saved.RoomDeviceSessionId);
        Assert.Equal(context.DeviceId, saved.DeviceId);
        Assert.Equal(context.ShiftAssignmentId, saved.StaffShiftAssignmentId);
        Assert.Equal(context.OperationId, saved.OperationId);
        Assert.Equal(entry.RecordId, saved.RecordId);
        Assert.Equal(entry.TimestampUtc, saved.TimestampUtc);
        using var before = JsonDocument.Parse(saved.OldValuesJson!);
        using var after = JsonDocument.Parse(saved.NewValuesJson!);
        Assert.Equal("*******678", before.RootElement.GetProperty("Values").GetProperty("PhoneNumber").GetString());
        Assert.Equal("*********567", after.RootElement.GetProperty("Values").GetProperty("NationalId").GetString());
        var metadata = after.RootElement.GetProperty("RoomContext");
        Assert.Equal(context.RoomId, metadata.GetProperty("RoomId").GetGuid());
        Assert.Equal(context.ShiftLeaderStaffId, metadata.GetProperty("ShiftLeaderStaffId").GetGuid());
        Assert.Equal("2026-11-17", metadata.GetProperty("ShiftDate").GetString());
        Assert.Equal("Morning", metadata.GetProperty("ShiftName").GetString());
        // TestDatabase rolls back the surrounding SQL transaction on disposal.
    }
}
