using DocumentsReporting.Application;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace DocumentsReporting.Infrastructure.Adapters;

public sealed class AuditLogAdapter(AuditLogWriter auditLogWriter, TimeProvider timeProvider) : IAuditLogPort
{
    public async Task LogAsync(
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
        if (!Enum.TryParse<ActionType>(actionType, ignoreCase: true, out var parsedAction))
        {
            parsedAction = ActionType.INSERT;
        }

        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            TableName = "ResultAttachment",
            RecordId = recordId,
            ActionType = parsedAction,
            ChangedColumns = changedColumns,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            RoomAccountId = roomAccountId,
            DeviceId = deviceId,
            RoomDeviceSessionId = roomDeviceSessionId,
            PerformedByUserId = userId,
            ChangeReason = changeReason,
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        await auditLogWriter.WriteAsync(entry, ct);
    }
}
