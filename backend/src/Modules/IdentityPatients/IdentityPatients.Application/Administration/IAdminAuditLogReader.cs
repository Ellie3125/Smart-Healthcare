using System.Text.Json;

namespace IdentityPatients.Application.Administration;

public sealed record AdminAuditLogEntry(Guid Id, DateTime OccurredAt, string ActorName, string ActorRole,
    string Action, string EntityType, Guid EntityId, JsonElement? BeforeJson, JsonElement? AfterJson);
public sealed record AdminAuditLogPage(IReadOnlyList<AdminAuditLogEntry> Items, bool HasMore);
public interface IAdminAuditLogReader
{
    Task<AdminAuditLogPage> ReadAsync(string? keyword, int page, CancellationToken ct);
}
