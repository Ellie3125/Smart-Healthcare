using Shared.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

// Job quét cả bảng Outbox/ProcessedEvent nên chạy cùng nhóm không song song với dispatcher; dữ liệu rollback cuối test.
[Collection(OutboxCollection.Name)]
public sealed class TechnicalDataCleanupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Removes_only_expired_technical_rows()
    {
        await using var db = await TestDatabase.OpenAsync();
        var old = DateTime.UtcNow.AddDays(-(TechnicalDataCleanupJob.RetentionDays + 1));
        var fresh = DateTime.UtcNow.AddDays(-1);

        var oldPublished = await Outbox(db, "Published", old);
        var freshPublished = await Outbox(db, "Published", fresh);
        var oldDeadLetter = await Outbox(db, "DeadLetter", old);
        var oldPending = await Outbox(db, "Pending", old);

        var eventOfPublished = await Processed(db, oldPublished, old);
        var eventOfDeadLetter = await Processed(db, oldDeadLetter, old);
        var eventWithoutOutbox = await Processed(db, Guid.CreateVersion7(), old);
        var freshEvent = await Processed(db, freshPublished, fresh);

        var expiredKey = await Idempotency(db, DateTime.UtcNow.AddDays(-2));
        var justExpiredKey = await Idempotency(db, DateTime.UtcNow.AddHours(-1));
        var liveKey = await Idempotency(db, DateTime.UtcNow.AddDays(1));

        await using (var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options)))
            Assert.True(await new TechnicalDataCleanupJob(shared).RunAsync(Ct) >= 4);

        Assert.Equal([freshPublished, oldDeadLetter, oldPending], await Remaining(db, "OutboxMessage", oldPublished, freshPublished, oldDeadLetter, oldPending));
        Assert.Equal([eventOfDeadLetter, freshEvent], await Remaining(db, "ProcessedEvent", eventOfPublished, eventOfDeadLetter, eventWithoutOutbox, freshEvent));
        Assert.Equal([justExpiredKey, liveKey], await Remaining(db, "ApiIdempotencyRecord", expiredKey, justExpiredKey, liveKey));
    }

    private static Task<Guid> Outbox(TestDatabase db, string status, DateTime processedAtUtc) =>
        db.InsertAsync("shared.OutboxMessage", new
        {
            Id = Guid.CreateVersion7(), EventType = "ResultPublished", AggregateType = "CLSResult", PayloadJson = "{}",
            Status = status, CreatedAtUtc = processedAtUtc, ProcessedAtUtc = processedAtUtc,
        });

    private static Task<Guid> Processed(TestDatabase db, Guid eventId, DateTime processedAtUtc) =>
        db.InsertAsync("shared.ProcessedEvent", new
        {
            Id = Guid.CreateVersion7(), EventId = eventId, HandlerName = "cleanup-test", ProcessedAtUtc = processedAtUtc,
        });

    private static Task<Guid> Idempotency(TestDatabase db, DateTime expiresAtUtc) =>
        db.InsertAsync("shared.ApiIdempotencyRecord", new
        {
            Id = Guid.CreateVersion7(), IdempotencyKey = Guid.NewGuid().ToString(), CreatedAtUtc = expiresAtUtc.AddDays(-1),
            ExpiresAtUtc = expiresAtUtc,
        });

    private static async Task<Guid[]> Remaining(TestDatabase db, string table, params Guid[] ids)
    {
        var rows = await db.QueryAsync(
            $"SELECT Id FROM shared.{table} WHERE Id IN ({string.Join(", ", ids.Select(x => $"'{x}'"))})");
        return ids.Where(id => rows.Contains(id.ToString(), StringComparer.OrdinalIgnoreCase)).ToArray();
    }
}
