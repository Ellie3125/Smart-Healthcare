using DocumentsReporting.Application;

namespace DocumentsReporting.Tests.Mocks;

public sealed class TestAuditLogPort : IAuditLogPort
{
    public sealed record LogEntry(
        Guid RecordId,
        string ActionType,
        string ChangedColumns,
        string? OldValuesJson,
        string? NewValuesJson,
        Guid? RoomAccountId,
        string? DeviceId,
        Guid? RoomDeviceSessionId,
        Guid? UserId,
        string? ChangeReason);

    public List<LogEntry> Logs { get; } = [];

    public Task LogAsync(
        Guid recordId,
        string actionType,
        string changedColumns,
        string? oldValuesJson,
        string? newValuesJson,
        Guid? roomAccountId = null,
        string? deviceId = null,
        Guid? roomDeviceSessionId = null,
        Guid? userId = null,
        string? changeReason = null,
        CancellationToken ct = default)
    {
        Logs.Add(new LogEntry(
            recordId,
            actionType,
            changedColumns,
            oldValuesJson,
            newValuesJson,
            roomAccountId,
            deviceId,
            roomDeviceSessionId,
            userId,
            changeReason));

        return Task.CompletedTask;
    }
}
