using Billing.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Billing.Infrastructure.Persistence;

/// <summary>Ghi shared.AuditLog cho thao tác tiền của Billing, dùng chung kết nối và transaction đang mở của BillingDbContext.</summary>
internal static class BillingAuditWriter
{
    public static async Task WriteAsync(BillingDbContext db, SharedDbContext shared, BillingAuditEntry entry, DateTime timestampUtc,
        CancellationToken ct)
    {
        var transaction = (db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("AuditLog thao tác tiền phải ghi trong transaction của Billing.")).GetDbTransaction();

        // Như OutboxWriter: phiếu thu/hoàn và AuditLog cùng commit hoặc cùng rollback.
        if (shared.Database.CurrentTransaction?.GetDbTransaction() != transaction)
        {
            if (shared.Database.CurrentTransaction is not null)
                await shared.Database.UseTransactionAsync(null, ct);
            if (shared.Database.GetDbConnection() != transaction.Connection)
                shared.Database.SetDbConnection(transaction.Connection, contextOwnsConnection: false);
            await shared.Database.UseTransactionAsync(transaction, ct);
        }

        await new AuditLogWriter(shared).WriteAsync(new AuditLog
        {
            PerformedByUserId = entry.PerformedByUserId,
            TableName = entry.TableName,
            RecordId = entry.RecordId,
            ActionType = entry.IsInsert ? ActionType.INSERT : ActionType.UPDATE,
            ClientIpAddress = entry.ClientIp,
            ChangedColumns = entry.ChangedColumns,
            NewValuesJson = entry.NewValuesJson,
            ChangeReason = entry.ChangeReason,
            TimestampUtc = timestampUtc,
        }, ct);
    }
}
