using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure;

public sealed record AdminAuditRow(Guid Id, DateTime OccurredAt, Guid? ActorId, string Action, string EntityType,
    Guid EntityId, JsonElement? BeforeJson, JsonElement? AfterJson);
public sealed record AdminAuditPage(IReadOnlyList<AdminAuditRow> Items, bool HasMore);

public sealed class AuditLogAdminReader(SharedDbContext db)
{
    public const int PageSize = 50;
    public async Task<AdminAuditPage> ReadAsync(string? keyword, int page, CancellationToken ct)
    {
        keyword = keyword?.Trim();
        if (page is < 1 or > 1000 || keyword?.Length > 100) throw new ArgumentException("Invalid audit query.");
        var query = db.Set<AuditLog>().AsNoTracking();
        if (!string.IsNullOrEmpty(keyword)) query = query.Where(a => a.TableName.Contains(keyword)
            || a.ChangedColumns.Contains(keyword) || (a.ChangeReason != null && a.ChangeReason.Contains(keyword)));
        var rows = await query.OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(ct);
        return new(rows.Take(PageSize).Select(a => new AdminAuditRow(a.Id, DateTime.SpecifyKind(a.TimestampUtc, DateTimeKind.Utc),
            a.PerformedByUserId, a.ActionType.ToString(), a.TableName, a.RecordId,
            SafePayload(a.TableName, a.OldValuesJson), SafePayload(a.TableName, a.NewValuesJson))).ToArray(), rows.Count > PageSize);
    }

    private static JsonElement? SafePayload(string table, string? json)
    {
        // Admin audit access does not grant access to clinical records or result files.
        if (json is null || table.StartsWith("clinical.", StringComparison.OrdinalIgnoreCase)
            || table.StartsWith("documents.", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            if (JsonNode.Parse(SensitiveDataMasker.MaskJson(json)!) is not JsonObject root) return null;
            RedactSecrets(root);
            return JsonSerializer.SerializeToElement(root);
        }
        catch (JsonException) { return null; }
    }

    private static void RedactSecrets(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var (key, value) in obj.ToList())
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("token", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Contains("apikey", StringComparison.OrdinalIgnoreCase))
                    obj[key] = "[REDACTED]";
                else RedactSecrets(value);
            }
        else if (node is JsonArray array) foreach (var item in array) RedactSecrets(item);
    }
}
