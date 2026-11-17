using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence;

public sealed record OutboxEventSnapshot(Guid Id, string EventType, string PayloadJson, DateTime CreatedAtUtc);

/// <summary>Read committed event snapshots with an optional JSON discriminator; no module tables are queried.</summary>
public sealed class StaffCallListReader(SharedDbContext shared)
{
    public Task<List<OutboxEventSnapshot>> ReadAsync(EventType[] types, string? discriminatorPath, string? discriminatorValue,
        int skip, int take, CancellationToken ct)
    {
        if (types.Length == 0 || types.Any(x => !Enum.IsDefined(x)) || skip < 0 || take is < 1 or > 101)
            throw new ArgumentException("Invalid event snapshot query.");
        // Event types come from validated enums, while every filter value remains a SQL parameter.
        var names = string.Join(",", types.Select(x => $"N'{x}'"));
        var sql = $$"""
            SELECT Id, EventType, PayloadJson, CreatedAtUtc FROM shared.OutboxMessage
            WHERE EventType IN ({{names}}) AND ISJSON(PayloadJson)=1
              AND (@path IS NULL OR JSON_VALUE(CASE WHEN ISJSON(PayloadJson)=1 THEN PayloadJson ELSE N'[]' END,@path) IS NULL
                   OR JSON_VALUE(CASE WHEN ISJSON(PayloadJson)=1 THEN PayloadJson ELSE N'[]' END,@path)=@value)
            ORDER BY CreatedAtUtc DESC,Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
            """;
        return shared.Database.SqlQueryRaw<OutboxEventSnapshot>(sql,
            new SqlParameter("@path", SqlDbType.NVarChar, 128) { Value = (object?)discriminatorPath ?? DBNull.Value },
            new SqlParameter("@value", SqlDbType.NVarChar, 128) { Value = (object?)discriminatorValue ?? DBNull.Value },
            new SqlParameter("@skip", skip), new SqlParameter("@take", take)).ToListAsync(ct);
    }
}
