using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Notifications.Application.Exceptions;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Exceptions;
using Notifications.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class WorkflowExceptionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Monitor_queries_can_use_the_existing_filtered_indexes()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        var capture = new QueryCapture();
        await using var workflow = fixture.CreateContext<WorkflowDbContext>(o => new(
            new DbContextOptionsBuilder<WorkflowDbContext>(o).AddInterceptors(capture).Options));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(
            new DbContextOptionsBuilder<SharedDbContext>(o).AddInterceptors(capture).Options));
        var clock = new Clock(DateTime.UtcNow);
        await new WorkflowExceptionMonitor(workflow, new OutboxAdministration(shared, new AuditLogWriter(shared), clock),
            clock, new UnusedUow(), new UnusedIdempotency()).GetAsync(100, Ct);
        Assert.Equal(2, capture.Queries.Count);
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using (var enable = new SqlCommand("SET SHOWPLAN_XML ON", connection))
            await enable.ExecuteNonQueryAsync(Ct);
        foreach (var (sql, parameters) in capture.Queries)
        {
            var isOutbox = sql.Contains("[shared].[OutboxMessage]", StringComparison.Ordinal);
            var index = isOutbox ? "IX_OutboxMessage_Unfinished" : "IX_Operation_Unfinished";
            // A forced plan proves index eligibility even when the test database is too small for the optimizer to prefer it.
            var declarations = string.Join("\n", parameters.Select(p => p.Value switch
            {
                DateTime value => $"DECLARE {p.ParameterName} datetime2 = '{value:yyyy-MM-ddTHH:mm:ss.fffffff}';",
                int value => $"DECLARE {p.ParameterName} int = {value};",
                _ => throw new InvalidOperationException("Unexpected monitor query parameter.")
            }));
            await using var plan = new SqlCommand(declarations + "\n" + sql + $" OPTION (TABLE HINT([o], INDEX([{index}])))", connection);
            TestContext.Current.TestOutputHelper!.WriteLine(plan.CommandText);
            var xml = (string)(await plan.ExecuteScalarAsync(Ct))!;
            Assert.Contains(index, xml);
            Assert.DoesNotContain("Lookup=\"true\"", xml);
        }
    }

    [Theory]
    [InlineData(1, "120", 120)]
    [InlineData(2, "120", 240)]
    [InlineData(5, "120", 1920)]
    [InlineData(10, "120", 61440)]
    [InlineData(11, "120", 61440)]
    [InlineData(2, "0", 0)]
    [InlineData(1, null, 30)]
    public async Task Retry_time_uses_last_failure_and_configured_exponential_delay(int attempts, string? delay, int seconds)
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var workflow = fixture.CreateContext<WorkflowDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var now = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var failedAt = now.AddMinutes(-1);
        var id = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed",
            AggregateType = "Appointment", Status = "Pending", RetryCount = attempts, CreatedAtUtc = now.AddDays(-1), ProcessedAtUtc = failedAt });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Outbox:RetryDelaySeconds"] = delay }).Build();
        await using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSharedInfrastructure(TestDatabase.RequiredConnectionString()).BuildServiceProvider();
        // Resolve the reader through DI to check the same configuration path used by the API.
        var reader = ActivatorUtilities.CreateInstance<OutboxAdministration>(services, shared, new AuditLogWriter(shared), new Clock(now));
        var monitor = new WorkflowExceptionMonitor(workflow, reader, new Clock(now), new UnusedUow(), new UnusedIdempotency());
        var item = Assert.Single((await monitor.GetAsync(100, Ct)).Items, x => x.Id == id);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(item));
        Assert.True(json.RootElement.TryGetProperty("NextRetryAtUtc", out var next), "Monitor must expose the next retry time.");
        Assert.Equal(failedAt.AddSeconds(seconds), next.GetDateTime());
    }

    [Fact]
    public async Task Lists_failed_and_stalled_operations_and_outbox_without_exposing_payloads_or_failure_text()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var workflow = fixture.CreateContext<WorkflowDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var now = DateTime.UtcNow;
        var clock = new Clock(now);
        var old = await fixture.InsertAsync("workflow.Operation", new { OperationType = "HoldSlot", TargetEntityType = "Reservation", Status = "Running", CreatedAtUtc = now.AddMinutes(-6) });
        var fresh = await fixture.InsertAsync("workflow.Operation", new { OperationType = "HoldSlot", TargetEntityType = "Reservation", Status = "Running", CreatedAtUtc = now });
        var failed = await fixture.InsertAsync("workflow.Operation", new { OperationType = "HoldSlot", TargetEntityType = "Reservation", Status = "Failed", FailureReason = "sensitive-error-text", CreatedAtUtc = now });
        var done = await fixture.InsertAsync("workflow.Operation", new { OperationType = "HoldSlot", TargetEntityType = "Reservation", Status = "Succeeded", CreatedAtUtc = now.AddHours(-1) });
        var dead = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed", AggregateType = "Appointment", Status = "DeadLetter", RetryCount = 5, PayloadJson = "sensitive-payload", CreatedAtUtc = now });
        var pending = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed", AggregateType = "Appointment", Status = "Pending", RetryCount = 0, PayloadJson = "sensitive-payload", CreatedAtUtc = now });
        var retrying = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed", AggregateType = "Appointment", Status = "Pending", RetryCount = 1, PayloadJson = "private", CreatedAtUtc = now });
        var staleProcessing = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed", AggregateType = "Appointment", Status = "Processing", RetryCount = 1, PayloadJson = "private", CreatedAtUtc = now.AddMinutes(-10), ProcessedAtUtc = now.AddMinutes(-6) });
        var freshProcessing = await fixture.InsertAsync("shared.OutboxMessage", new { EventType = "AppointmentConfirmed", AggregateType = "Appointment", Status = "Processing", RetryCount = 1, PayloadJson = "private", CreatedAtUtc = now.AddMinutes(-10), ProcessedAtUtc = now });
        var monitor = new WorkflowExceptionMonitor(workflow, new OutboxAdministration(shared, new AuditLogWriter(shared), clock), clock, new UnusedUow(), new UnusedIdempotency());
        var result = await monitor.GetAsync(100, Ct);
        Assert.Contains(result.Items, x => x.Id == old && x.Kind == "OperationStalled");
        Assert.Contains(result.Items, x => x.Id == failed);
        Assert.Contains(result.Items, x => x.Id == dead && x.CanRetryOutbox);
        Assert.Contains(result.Items, x => x.Id == retrying && !x.CanRetryOutbox);
        Assert.Contains(result.Items, x => x.Id == staleProcessing && !x.CanRetryOutbox);
        Assert.All(result.Items, x => Assert.Null(x.NextRetryAtUtc));
        Assert.DoesNotContain(result.Items, x => x.Id == fresh || x.Id == done || x.Id == pending || x.Id == freshProcessing);
        Assert.Contains("Billing.PaidUnallocated", result.UnavailableSources);
        Assert.Contains("Scheduling.Coordination", result.UnavailableSources);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("sensitive-", json);
        Assert.DoesNotContain("PayloadJson", json);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => monitor.GetAsync(101, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => monitor.GetAsync(0, Ct));
    }

    [Fact]
    public async Task Parallel_retry_replays_once_and_keeps_event_identity_payload_and_processed_markers()
    {
        var cs = TestDatabase.RequiredConnectionString();
        await using var services = new ServiceCollection().AddSharedInfrastructure(cs).AddNotificationsInfrastructure(cs).BuildServiceProvider();
        var message = new OutboxMessage { Id = Guid.NewGuid(), EventType = EventType.AppointmentConfirmed, AggregateType = AggregateType.Appointment,
            AggregateId = Guid.NewGuid(), PayloadJson = "private-payload", Status = OutboxMessageStatus.DeadLetter, RetryCount = 5, CreatedAtUtc = DateTime.UtcNow };
        var actor = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("N");
        try
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
                db.Add(message);
                db.Add(new ProcessedEvent { Id = Guid.NewGuid(), EventId = message.Id, HandlerName = "AlreadySucceeded", ProcessedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync(Ct);
            }
            async Task<RetryExceptionResult> Retry(string note, string idempotencyKey)
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IWorkflowExceptionMonitor>().RetryOutboxAsync(message.Id, actor, note, idempotencyKey, Ct);
            }
            var results = await Task.WhenAll(Retry("Đã kiểm nguyên nhân", key), Retry("Đã kiểm nguyên nhân", key));
            Assert.All(results, x => Assert.Equal(RetryExceptionResult.Retried, x));
            Assert.Equal(RetryExceptionResult.Conflict, await Retry("Đổi payload của khóa cũ", key));
            Assert.Equal(RetryExceptionResult.Conflict, await Retry("Đã kiểm nguyên nhân", Guid.NewGuid().ToString("N")));
            Assert.Equal(RetryExceptionResult.InvalidInput, await Retry("", "bad"));
            Assert.Equal(RetryExceptionResult.InvalidInput, await Retry(new('x', 501), "bad"));
            Assert.Equal(RetryExceptionResult.InvalidInput, await Retry("Valid", new('x', 129)));
            Assert.Equal(RetryExceptionResult.InvalidInput, await Retry("Valid", "invalid\nkey"));
            await using var read = services.CreateAsyncScope();
            var monitorRead = read.ServiceProvider.GetRequiredService<IWorkflowExceptionMonitor>();
            Assert.Equal(RetryExceptionResult.NotFound, await monitorRead.RetryOutboxAsync(Guid.NewGuid(), actor, "Valid", "missing-message", Ct));
            Assert.Equal(RetryExceptionResult.InvalidInput, await monitorRead.RetryOutboxAsync(message.Id, Guid.Empty, "Valid", "empty-actor", Ct));
            var dbRead = read.ServiceProvider.GetRequiredService<SharedDbContext>();
            var saved = await dbRead.Set<OutboxMessage>().SingleAsync(x => x.Id == message.Id, Ct);
            Assert.Equal(OutboxMessageStatus.Pending, saved.Status);
            Assert.Equal(0, saved.RetryCount);
            Assert.Null(saved.ProcessedAtUtc);
            Assert.Equal(message.PayloadJson, saved.PayloadJson);
            Assert.Equal(message.AggregateId, saved.AggregateId);
            Assert.Equal(1, await dbRead.Set<AuditLog>().CountAsync(x => x.RecordId == message.Id, Ct));
            Assert.Equal(1, await dbRead.Set<ProcessedEvent>().CountAsync(x => x.EventId == message.Id, Ct));
        }
        finally
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            await db.Set<AuditLog>().Where(x => x.RecordId == message.Id).ExecuteDeleteAsync(Ct);
            await db.Set<ProcessedEvent>().Where(x => x.EventId == message.Id).ExecuteDeleteAsync(Ct);
            await db.Set<OutboxMessage>().Where(x => x.Id == message.Id).ExecuteDeleteAsync(Ct);
            var caller = "AppUser:" + actor.ToString("D");
            await db.Set<ApiIdempotencyRecord>().Where(x => x.CallerScope == caller).ExecuteDeleteAsync(Ct);
        }
    }

    private sealed class Clock(DateTime now) : IDbClock { public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(now); }
    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<(string Sql, SqlParameter[] Parameters)> Queries { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries.Add((command.CommandText, command.Parameters.Cast<SqlParameter>()
                .Select(x => (SqlParameter)((ICloneable)x).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
    private sealed class UnusedUow : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => throw new NotSupportedException();
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class UnusedIdempotency : IIdempotencyStore
    {
        public Task<IdempotencyBeginResult> BeginAsync(string a, string b, string c, string d, TimeSpan e, Guid? f, CancellationToken ct) => throw new NotSupportedException();
        public Task CompleteAsync(string a, string b, string c, int d, string? e, Guid? f, CancellationToken ct) => throw new NotSupportedException();
    }
}
