namespace DocumentsReporting.Application;

public interface IAuditLogPort
{
    Task LogAsync(
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
        CancellationToken ct = default);
}
