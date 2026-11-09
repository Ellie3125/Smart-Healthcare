using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Xunit;

namespace SqlServerTests;

// Chạy riêng: các test này commit thật vào SmartHealthcare_Test (rồi tự xóa), không chạy song song với class khác.
[CollectionDefinition(nameof(SharedInfrastructureSerialCollection), DisableParallelization = true)]
public sealed class SharedInfrastructureSerialCollection;

// W1-06: UnitOfWork, publisher, ProcessedEvent, idempotency, giờ DB, AuditLog trên SQL Server thật.
[Collection(nameof(SharedInfrastructureSerialCollection))]
public class SharedInfrastructureTests
{
    // ---------- UnitOfWork ----------

    [Fact]
    public async Task Unit_of_work_commits_both_participants_together()
    {
        await using var services = BuildServices();
        var (serviceId, operationId) = (Guid.NewGuid(), Guid.NewGuid());
        try
        {
            await using var scope = services.CreateAsyncScope();
            var (scheduling, workflow) = Contexts(scope);

            await Uow(scope).ExecuteAsync(async t =>
            {
                scheduling.Add(Service(serviceId));
                workflow.Add(Operation(operationId));
                await scheduling.SaveChangesAsync(t);
                await workflow.SaveChangesAsync(t);
            }, Ct);

            Assert.Equal((1, 1), await CountAsync(serviceId, operationId));
        }
        finally
        {
            await CleanupAsync(serviceId, operationId);
        }
    }

    [Fact]
    public async Task Unit_of_work_rolls_back_both_participants_when_work_fails()
    {
        await using var services = BuildServices();
        var (serviceId, operationId) = (Guid.NewGuid(), Guid.NewGuid());
        var runs = 0;
        try
        {
            await using var scope = services.CreateAsyncScope();
            var (scheduling, workflow) = Contexts(scope);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Uow(scope).ExecuteAsync(async t =>
            {
                runs++;
                scheduling.Add(Service(serviceId));
                await scheduling.SaveChangesAsync(t);
                workflow.Add(Operation(operationId));
                await workflow.SaveChangesAsync(t);
                throw new InvalidOperationException("lỗi nghiệp vụ giữa chừng");
            }, Ct));

            Assert.Equal(1, runs); // lỗi khác 1205 không retry
            Assert.Equal((0, 0), await CountAsync(serviceId, operationId));
        }
        finally
        {
            await CleanupAsync(serviceId, operationId);
        }
    }

    [Fact]
    public async Task Nested_unit_of_work_joins_outer_transaction_and_does_not_commit_early()
    {
        await using var services = BuildServices();
        var (serviceId, operationId) = (Guid.NewGuid(), Guid.NewGuid());
        try
        {
            await using var scope = services.CreateAsyncScope();
            var (scheduling, workflow) = Contexts(scope);
            var uow = Uow(scope);

            await Assert.ThrowsAsync<RollbackSignal>(() => uow.ExecuteAsync(async t =>
            {
                scheduling.Add(Service(serviceId));
                await scheduling.SaveChangesAsync(t);
                var inner = await uow.ExecuteAsync(async t2 =>
                {
                    workflow.Add(Operation(operationId));
                    await workflow.SaveChangesAsync(t2);
                    return 42;
                }, t);
                Assert.Equal(42, inner);
                throw new RollbackSignal();
            }, Ct));

            // Lời gọi lồng đã commit sớm thì Operation còn lại sau khi bên ngoài rollback.
            Assert.Equal((0, 0), await CountAsync(serviceId, operationId));
        }
        finally
        {
            await CleanupAsync(serviceId, operationId);
        }
    }

    [Fact]
    public async Task Real_deadlock_victim_is_retried_from_scratch_and_succeeds()
    {
        await using var services = BuildServices();
        var (rowA, rowB) = (Guid.NewGuid(), Guid.NewGuid());
        await ExecAsync($"""
            INSERT INTO shared.ProcessedEvent (Id, EventId, HandlerName, ProcessedAtUtc) VALUES
              ('{rowA}', NEWID(), 'w1-06-deadlock-a', SYSUTCDATETIME()), ('{rowB}', NEWID(), 'w1-06-deadlock-b', SYSUTCDATETIME())
            """);
        try
        {
            var aLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Phiên kia: khóa B, chờ UoW khóa A, rồi xin A -> vòng chờ với UoW.
            var other = Task.Run(async () =>
            {
                await using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync(Ct);
                // UoW làm nạn nhân deadlock để kết quả xác định. Lệnh không tham số để SET giữ cho cả phiên.
                await using (var priority = new SqlCommand("SET DEADLOCK_PRIORITY HIGH", connection))
                    await priority.ExecuteNonQueryAsync(Ct);
                await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(Ct);
                await TouchAsync(connection, tx, rowB);
                bLocked.SetResult();
                await aLocked.Task;
                await TouchAsync(connection, tx, rowA);
                await tx.CommitAsync(Ct);
            }, Ct);

            await using var scope = services.CreateAsyncScope();
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            var runs = 0;
            await Uow(scope).ExecuteAsync(async t =>
            {
                runs++;
                await shared.Database.ExecuteSqlAsync($"UPDATE shared.ProcessedEvent SET ProcessedAtUtc = SYSUTCDATETIME() WHERE Id = {rowA}", t);
                if (runs == 1)
                {
                    aLocked.SetResult();
                    await bLocked.Task;
                }

                await shared.Database.ExecuteSqlAsync($"UPDATE shared.ProcessedEvent SET ProcessedAtUtc = SYSUTCDATETIME() WHERE Id = {rowB}", t);
            }, Ct);
            await other;

            Assert.Equal(2, runs);
        }
        finally
        {
            await ExecAsync($"DELETE FROM shared.ProcessedEvent WHERE Id IN ('{rowA}', '{rowB}')");
        }
    }

    [Fact]
    public async Task Deadlock_wrapped_in_DbUpdateException_is_retried_with_a_clean_change_tracker()
    {
        await using var services = BuildServices();
        var (discarded, kept, operationId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var runs = 0;
        try
        {
            await using var scope = services.CreateAsyncScope();
            var (scheduling, workflow) = Contexts(scope);

            await Uow(scope).ExecuteAsync(async t =>
            {
                runs++;
                if (runs < 3)
                {
                    scheduling.Add(Service(discarded)); // chưa lưu; lần chạy lại không được mang theo
                    throw new DbUpdateException("deadlock giả", FakeSqlException.Create(1205));
                }

                scheduling.Add(Service(kept));
                workflow.Add(Operation(operationId));
                await scheduling.SaveChangesAsync(t);
                await workflow.SaveChangesAsync(t);
            }, Ct);

            Assert.Equal(3, runs);
            Assert.Equal((1, 1), await CountAsync(kept, operationId));
            Assert.Equal((0, 0), await CountAsync(discarded, Guid.Empty));
        }
        finally
        {
            await CleanupAsync(kept, operationId);
            await CleanupAsync(discarded, Guid.Empty);
        }
    }

    [Fact]
    public async Task Deadlock_retry_stops_after_three_retries()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var runs = 0;

        var error = await Assert.ThrowsAsync<SqlException>(() => Uow(scope).ExecuteAsync(_ =>
        {
            runs++;
            throw FakeSqlException.Create(1205);
        }, Ct));

        Assert.Equal(1205, error.Number);
        Assert.Equal(4, runs); // 1 lần chạy + 3 lần thử lại
    }

    [Fact]
    public async Task Other_sql_errors_are_not_retried()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var runs = 0;

        var error = await Assert.ThrowsAsync<SqlException>(() => Uow(scope).ExecuteAsync(_ =>
        {
            runs++;
            throw FakeSqlException.Create(TestDatabase.CheckViolation);
        }, Ct));

        Assert.Equal(TestDatabase.CheckViolation, error.Number);
        Assert.Equal(1, runs);
    }

    // ---------- Publisher ----------

    [Fact]
    public async Task Publisher_writes_outbox_inside_the_transaction()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var (e, aggregateId) = (new AppointmentConfirmed(Guid.NewGuid(), 1, DateTime.UtcNow, null), Guid.NewGuid());

        await InRollbackAsync(scope, async t =>
        {
            await publisher.PublishAsync(e, aggregateId, t);

            var row = await shared.Database.SqlQuery<string>($"""
                SELECT EventType + '|' + AggregateType + '|' + CONVERT(varchar(36), AggregateId) + '|' + Status AS Value
                FROM shared.OutboxMessage WHERE Id = {e.EventId}
                """).ToListAsync(t);
            Assert.Equal([$"AppointmentConfirmed|Appointment|{aggregateId.ToString().ToUpperInvariant()}|Pending"], row);
        });
    }

    [Fact]
    public async Task Publisher_outside_a_transaction_is_rejected()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(new AppointmentConfirmed(Guid.NewGuid(), 1, DateTime.UtcNow, null), Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Publisher_rejects_an_event_without_an_outbox_type()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        await InRollbackAsync(scope, t => Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishAsync(new UnknownHappened(Guid.NewGuid(), 1, DateTime.UtcNow, null), Guid.NewGuid(), t)));
    }

    // ---------- ProcessedEvent ----------

    [Fact]
    public async Task Processed_event_is_marked_once_per_handler()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessedEventStore>();
        var eventId = Guid.NewGuid();

        await InRollbackAsync(scope, async t =>
        {
            Assert.True(await store.TryMarkAsync(eventId, "HandlerA", t));
            Assert.False(await store.TryMarkAsync(eventId, "HandlerA", t));
            Assert.True(await store.TryMarkAsync(eventId, "HandlerB", t));
        });
    }

    [Fact]
    public async Task Processed_event_outside_a_transaction_is_rejected()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessedEventStore>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TryMarkAsync(Guid.NewGuid(), "HandlerA", Ct));
    }

    // ---------- Idempotency ----------

    [Fact]
    public async Task Idempotency_started_then_completed_then_replayed()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var (key, operationId) = (Guid.NewGuid().ToString("N"), Guid.NewGuid());

        await InRollbackAsync(scope, async t =>
        {
            Assert.IsType<IdempotencyBeginResult.Started>(await Begin(store, key, "hash-1", Hour, operationId, t));
            await store.CompleteAsync(Scope, OperationType, key, 201, """{"ticket":"A01"}""", operationId, t);

            var replay = Assert.IsType<IdempotencyBeginResult.Replay>(await Begin(store, key, "hash-1", Hour, null, t));
            Assert.Equal(new IdempotencyBeginResult.Replay(201, """{"ticket":"A01"}""", operationId), replay);
        });
    }

    [Fact]
    public async Task Idempotency_same_key_with_different_payload_is_a_conflict()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = Guid.NewGuid().ToString("N");

        await InRollbackAsync(scope, async t =>
        {
            await Begin(store, key, "hash-1", Hour, null, t);
            await store.CompleteAsync(Scope, OperationType, key, 200, "{}", null, t);

            Assert.IsType<IdempotencyBeginResult.Conflict>(await Begin(store, key, "hash-2", Hour, null, t));
        });
    }

    [Fact]
    public async Task Idempotency_record_without_result_is_in_progress_not_success()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var (key, operationId) = (Guid.NewGuid().ToString("N"), Guid.NewGuid());

        await InRollbackAsync(scope, async t =>
        {
            await Begin(store, key, "hash-1", Hour, operationId, t);

            Assert.Equal(new IdempotencyBeginResult.InProgress(operationId), await Begin(store, key, "hash-1", Hour, null, t));
        });
    }

    [Fact]
    public async Task Idempotency_expired_record_is_overwritten()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        var key = Guid.NewGuid().ToString("N");

        await InRollbackAsync(scope, async t =>
        {
            await Begin(store, key, "hash-old", TimeSpan.Zero, null, t); // hết hạn ngay theo giờ DB
            await store.CompleteAsync(Scope, OperationType, key, 200, "{}", null, t);

            Assert.IsType<IdempotencyBeginResult.Started>(await Begin(store, key, "hash-new", Hour, null, t));
            Assert.IsType<IdempotencyBeginResult.InProgress>(await Begin(store, key, "hash-new", Hour, null, t));
            Assert.Equal([1], await shared.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS Value FROM shared.ApiIdempotencyRecord WHERE IdempotencyKey = {key}").ToListAsync(t));
        });
    }

    [Fact]
    public async Task Idempotency_outside_a_transaction_is_rejected()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Begin(store, "k", "h", Hour, null, Ct));
    }

    // ---------- Giờ DB và AuditLog ----------

    [Fact]
    public async Task Db_clock_returns_sql_server_utc_time()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var clock = scope.ServiceProvider.GetRequiredService<IDbClock>();

        var now = await clock.GetUtcNowAsync(Ct);

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.InRange(now, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task Audit_log_is_stored_with_national_id_and_phone_masked()
    {
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<AuditLogWriter>();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        var id = Guid.NewGuid();

        await InRollbackAsync(scope, async t =>
        {
            await writer.WriteAsync(new AuditLog
            {
                Id = id,
                TableName = "identity.Patient",
                RecordId = Guid.NewGuid(),
                ActionType = ActionType.UPDATE,
                ChangedColumns = "PhoneNumber",
                OldValuesJson = """{"phoneNumber":"0912345678"}""",
                NewValuesJson = """{"PhoneNumber":"0987654321","NationalId":"001203004567"}""",
                TimestampUtc = DateTime.UtcNow,
            }, t);

            Assert.Equal(["""{"phoneNumber":"*******678"}|{"PhoneNumber":"*******321","NationalId":"*********567"}"""],
                await shared.Database.SqlQuery<string>(
                    $"SELECT OldValuesJson + '|' + NewValuesJson AS Value FROM shared.AuditLog WHERE Id = {id}").ToListAsync(t));
        });
    }

    // ---------- Hỗ trợ ----------

    public sealed record AppointmentConfirmed(Guid EventId, int Version, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent;

    public sealed record UnknownHappened(Guid EventId, int Version, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent;

    private sealed class RollbackSignal : Exception;

    private const string Scope = "patient:w1-06-test";
    private const string OperationType = "HoldSlot";
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static string ConnectionString => TestDatabase.RequiredConnectionString();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider BuildServices()
    {
        var cs = ConnectionString;
        return new ServiceCollection()
            .AddSharedInfrastructure(cs)
            .AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            .BuildServiceProvider(validateScopes: true);
    }

    private static IUnitOfWork Uow(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    private static (SchedulingDbContext, WorkflowDbContext) Contexts(AsyncServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<SchedulingDbContext>(), scope.ServiceProvider.GetRequiredService<WorkflowDbContext>());

    private static Task<IdempotencyBeginResult> Begin(IIdempotencyStore store, string key, string hash, TimeSpan ttl, Guid? operationId, CancellationToken ct) =>
        store.BeginAsync(Scope, OperationType, key, hash, ttl, operationId, ct);

    /// <summary>Chạy trong UoW rồi rollback, để không để lại dữ liệu.</summary>
    private static Task InRollbackAsync(AsyncServiceScope scope, Func<CancellationToken, Task> body) =>
        Assert.ThrowsAsync<RollbackSignal>(() => Uow(scope).ExecuteAsync(async t =>
        {
            await body(t);
            throw new RollbackSignal();
        }, Ct));

    private static MedicalService Service(Guid id) => new()
    {
        Id = id,
        ServiceCode = $"W106-{id:N}",
        ServiceName = "Dịch vụ test W1-06",
        DefaultRoomId = Guid.NewGuid(),
        IsActive = true,
    };

    private static Operation Operation(Guid id) => new()
    {
        Id = id,
        InitiatedByUserId = Guid.NewGuid(),
        OperationType = Notifications.Domain.Enums.OperationType.HoldSlot,
        TargetEntityType = TargetEntityType.BookingAttempt,
        TargetEntityId = Guid.NewGuid(),
        Status = OperationStatus.Pending,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private static async Task<(int Services, int Operations)> CountAsync(Guid serviceId, Guid operationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(
            "SELECT (SELECT COUNT(*) FROM scheduling.MedicalService WHERE Id = @s), (SELECT COUNT(*) FROM workflow.Operation WHERE Id = @o)", connection);
        command.Parameters.AddWithValue("@s", serviceId);
        command.Parameters.AddWithValue("@o", operationId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        await reader.ReadAsync(Ct);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static Task CleanupAsync(Guid serviceId, Guid operationId) => ExecAsync(
        $"DELETE FROM scheduling.MedicalService WHERE Id = '{serviceId}'; DELETE FROM workflow.Operation WHERE Id = '{operationId}'");

    private static async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task TouchAsync(SqlConnection connection, SqlTransaction tx, Guid id)
    {
        await using var command = new SqlCommand("UPDATE shared.ProcessedEvent SET ProcessedAtUtc = SYSUTCDATETIME() WHERE Id = @id", connection, tx);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
