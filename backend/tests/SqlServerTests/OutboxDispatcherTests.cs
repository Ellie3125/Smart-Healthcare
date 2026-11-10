using System.Collections.Concurrent;
using System.Text.Json;
using Billing.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.Confirmations;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Outbox;
using Xunit;

namespace SqlServerTests;

// Dispatcher quét toàn bảng OutboxMessage nên chạy riêng, sau các test song song (dữ liệu commit thật, dọn ở finally).
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OutboxCollection
{
    public const string Name = "Outbox dispatcher";
}

/// <summary>Sự kiện thử, gửi dưới EventType ResultPublished (W3-00 chưa có handler thật cho loại này).</summary>
public sealed record ResultPublished(Guid EventId, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent
{
    public int Version => 1;
}

[Collection(OutboxCollection.Name)]
public sealed class OutboxDispatcherTests
{
    private static readonly DateOnly ServiceDate = new(2099, 2, 20);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Transient_failure_is_retried_after_backoff()
    {
        var probe = new Probe();
        await using var services = ProbeServices(probe, new OutboxOptions { MaxAttempts = 5, RetryDelaySeconds = 3600 });
        var id = await InsertProbeAsync();
        probe.Failures[id] = 1;
        try
        {
            await DispatchAsync(services);
            Assert.Equal(("Pending", 1), await ReadAsync(id));

            await DispatchAsync(services); // chưa hết backoff 1 giờ theo giờ DB
            Assert.Equal(1, probe.Calls[id]);

            await ExecAsync($"UPDATE shared.OutboxMessage SET ProcessedAtUtc = DATEADD(SECOND, -3601, SYSUTCDATETIME()) WHERE Id = '{id}'");
            await DispatchAsync(services);
            Assert.Equal(("Published", 1), await ReadAsync(id));
            Assert.Equal(2, probe.Calls[id]);
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task Failing_beyond_the_limit_moves_to_dead_letter()
    {
        var probe = new Probe();
        await using var services = ProbeServices(probe, new OutboxOptions { MaxAttempts = 3, RetryDelaySeconds = 0 });
        var id = await InsertProbeAsync();
        probe.Failures[id] = int.MaxValue;
        try
        {
            await DispatchAsync(services);
            Assert.Equal(("DeadLetter", 3), await ReadAsync(id));
            Assert.Equal(3, probe.Calls[id]);

            await DispatchAsync(services); // DeadLetter chờ xử lý tay, không thử lại
            Assert.Equal(3, probe.Calls[id]);
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task Two_dispatchers_in_parallel_handle_each_message_once()
    {
        var probe = new Probe { Delay = TimeSpan.FromMilliseconds(20) };
        var ids = new List<Guid>();
        await using var first = ProbeServices(probe, new OutboxOptions { BatchSize = 5 });
        await using var second = ProbeServices(probe, new OutboxOptions { BatchSize = 5 });
        try
        {
            for (var i = 0; i < 40; i++)
                ids.Add(await InsertProbeAsync());

            await Task.WhenAll(Task.Run(() => DispatchAsync(first), Ct), Task.Run(() => DispatchAsync(second), Ct));

            Assert.All(ids, id => Assert.Equal(1, probe.Calls[id]));
            foreach (var id in ids)
                Assert.Equal(("Published", 0), await ReadAsync(id));
        }
        finally
        {
            await DeleteAsync([.. ids]);
        }
    }

    [Fact]
    public async Task Message_left_processing_by_a_dead_worker_is_reclaimed_after_the_lease()
    {
        var probe = new Probe();
        await using var services = ProbeServices(probe, new OutboxOptions { LeaseSeconds = 60 });
        var fresh = await InsertProbeAsync("Processing", processedSecondsAgo: 30);
        var stale = await InsertProbeAsync("Processing", processedSecondsAgo: 61);
        try
        {
            await DispatchAsync(services);

            Assert.Equal(("Processing", 0), await ReadAsync(fresh));
            Assert.Equal(("Published", 1), await ReadAsync(stale)); // lần bị bỏ dở tính là một lần thử
            Assert.False(probe.Calls.ContainsKey(fresh));
        }
        finally
        {
            await DeleteAsync(fresh, stale);
        }
    }

    [Fact]
    public async Task Payment_verified_through_the_dispatcher_confirms_once_even_when_delivered_twice()
    {
        await using var services = SchedulingServices();
        var day = await SeedDayAsync(services);
        var paid = Guid.Empty;
        try
        {
            var attempt = await HeldAttemptAsync(services, day);
            var e = new PaymentVerified(Guid.CreateVersion7(), Guid.NewGuid(), PaymentPurpose.InitialConsultation, Guid.NewGuid(),
                attempt, null, [Guid.NewGuid()], DateTime.UtcNow, null);
            paid = e.EventId;
            await using (var scope = services.CreateAsyncScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
                    .ExecuteAsync(t => publisher.PublishAsync(e, e.PaymentIntentId, t), Ct);
            }

            await DispatchAsync(services);
            Assert.Equal(("Published", 0), await ReadAsync(paid));

            // Worker chết sau khi handler commit, trước khi đánh dấu Published: sự kiện được giao lại.
            await ExecAsync($"UPDATE shared.OutboxMessage SET Status = N'Pending' WHERE Id = '{paid}'");
            await DispatchAsync(services);

            Assert.Equal(("Published", 0), await ReadAsync(paid));
            await using var check = services.CreateAsyncScope();
            var db = check.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var appointment = await db.Set<Appointment>().SingleAsync(x => x.SessionQuotaId == day.Id, Ct);
            Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
            Assert.Equal(BookingAttemptStatus.Confirmed, await db.Set<BookingAttempt>().Where(x => x.Id == attempt).Select(x => x.Status).SingleAsync(Ct));
            var quota = await db.Set<DoctorDayQuota>().AsNoTracking().SingleAsync(x => x.Id == day.Id, Ct);
            Assert.Equal((0, 1), (quota.HeldCount, quota.AdmittedCount));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.ProcessedEvent WHERE EventId = '{paid}'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{appointment.Id}' AND EventType = N'AppointmentConfirmed'"));
        }
        finally
        {
            await CleanupSchedulingAsync(day, paid);
        }
    }

    // ---------- dispatcher với sự kiện thử ----------

    private sealed class Probe
    {
        public ConcurrentDictionary<Guid, int> Calls { get; } = new();
        public ConcurrentDictionary<Guid, int> Failures { get; } = new();
        public TimeSpan Delay { get; init; }
    }

    private sealed class ProbeHandler(Probe probe) : IIntegrationEventHandler<ResultPublished>
    {
        public async Task HandleAsync(ResultPublished e, CancellationToken ct)
        {
            var call = probe.Calls.AddOrUpdate(e.EventId, 1, (_, n) => n + 1);
            await Task.Delay(probe.Delay, ct);
            if (call <= probe.Failures.GetValueOrDefault(e.EventId))
                throw new InvalidOperationException("Lỗi tạm thời giả lập.");
        }
    }

    private static ServiceProvider ProbeServices(Probe probe, OutboxOptions options) =>
        new ServiceCollection()
            .AddSharedInfrastructure(TestDatabase.RequiredConnectionString())
            .AddOutboxDispatcher(options)
            .AddOutboxEvent<ResultPublished>()
            .AddSingleton(probe)
            .AddScoped<IIntegrationEventHandler<ResultPublished>, ProbeHandler>()
            .BuildServiceProvider(validateScopes: true);

    private static async Task DispatchAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchPendingAsync(Ct);
    }

    private static async Task<Guid> InsertProbeAsync(string status = "Pending", int? processedSecondsAgo = null)
    {
        var id = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(new ResultPublished(id, DateTime.UtcNow, null));
        var processed = processedSecondsAgo is { } s ? $"DATEADD(SECOND, -{s}, SYSUTCDATETIME())" : "NULL";
        await ExecAsync($"""
            INSERT INTO shared.OutboxMessage (Id, OperationId, EventType, AggregateType, AggregateId, PayloadJson, RetryCount, Status, CreatedAtUtc, ProcessedAtUtc)
            VALUES ('{id}', NULL, N'ResultPublished', N'CLSResult', NEWID(), N'{payload}', 0, N'{status}', SYSUTCDATETIME(), {processed})
            """);
        return id;
    }

    private static async Task<(string Status, int RetryCount)> ReadAsync(Guid id)
    {
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand($"SELECT Status, RetryCount FROM shared.OutboxMessage WHERE Id = '{id}'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetString(0), reader.GetInt32(1));
    }

    private static Task DeleteAsync(params Guid[] ids) => ids.Length == 0 ? Task.CompletedTask
        : ExecAsync($"DELETE FROM shared.OutboxMessage WHERE Id IN ({string.Join(", ", ids.Select(x => $"'{x}'"))})");

    // ---------- PaymentVerified → xác nhận lịch (handler W2-06) ----------

    private static ServiceProvider SchedulingServices()
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection()
            .AddSharedInfrastructure(cs)
            .AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddSchedulingApplication()
            .AddSingleton<IBillingModule>(new FixedFeeBilling())
            .AddOutboxDispatcher(new OutboxOptions())
            .AddOutboxEvent<PaymentVerified>()
            .BuildServiceProvider(validateScopes: true);
    }

    private static async Task<DoctorDayQuota> SeedDayAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var now = await scope.ServiceProvider.GetRequiredService<IDbClock>().GetUtcNowAsync(Ct);
        var quota = new DoctorDayQuota
        {
            Id = Guid.CreateVersion7(),
            DoctorId = Guid.NewGuid(),
            AssignedRoomId = Guid.NewGuid(),
            ServiceDate = ServiceDate,
            Capacity = 30,
            SessionStartTimeUtc = now.AddHours(2),
            BookingCutoffTimeUtc = now.AddHours(1),
            RescheduleCutoffTimeUtc = now.AddMinutes(30),
            SessionStatus = SessionStatus.Scheduled,
            IsOpen = true,
        };
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        db.Add(quota);
        await db.SaveChangesAsync(Ct);
        return quota;
    }

    private static async Task<Guid> HeldAttemptAsync(ServiceProvider services, DoctorDayQuota day)
    {
        var patient = Guid.NewGuid();
        var attempt = new BookingAttempt
        {
            Id = Guid.CreateVersion7(),
            AppUserId = Guid.NewGuid(),
            PatientId = patient,
            CurrentStep = CurrentStep.DoctorSelection,
            SelectedServiceDate = ServiceDate,
            Status = BookingAttemptStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        db.Add(attempt);
        await db.SaveChangesAsync(Ct);

        var held = await scope.ServiceProvider.GetRequiredService<HoldService>().HoldAsync(new HoldRequest(attempt.Id, patient,
            Guid.NewGuid(), day.DoctorId, Guid.NewGuid(), ServiceDate, BookingChannel.Web, Guid.NewGuid().ToString("N")), Ct);
        Assert.Equal(HoldResult.Held, held.Result);
        return attempt.Id;
    }

    private static Task CleanupSchedulingAsync(DoctorDayQuota day, Guid paidEventId) => ExecAsync($"""
        DECLARE @attempts TABLE (Id uniqueidentifier, AppUserId uniqueidentifier);
        INSERT INTO @attempts SELECT Id, AppUserId FROM scheduling.BookingAttempt WHERE SelectedServiceDate = '{ServiceDate:yyyy-MM-dd}';
        DECLARE @ops TABLE (Id uniqueidentifier);
        INSERT INTO @ops SELECT Id FROM workflow.Operation WHERE BookingAttemptId IN (SELECT Id FROM @attempts);
        DELETE FROM shared.ApiIdempotencyRecord WHERE OperationId IN (SELECT Id FROM @ops)
          OR CallerScope IN (SELECT 'AppUser:' + LOWER(CONVERT(nvarchar(36), AppUserId)) FROM @attempts);
        DELETE FROM workflow.Operation WHERE Id IN (SELECT Id FROM @ops);
        DELETE FROM shared.OutboxMessage WHERE Id = '{paidEventId}'
          OR AggregateId IN (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId = '{day.Id}');
        DELETE FROM shared.ProcessedEvent WHERE EventId = '{paidEventId}';
        DELETE FROM scheduling.ActiveBookingClaim WHERE SessionQuotaId = '{day.Id}';
        DELETE FROM shared.AuditLog WHERE RecordId IN (SELECT Id FROM scheduling.Appointment WHERE SessionQuotaId = '{day.Id}');
        DELETE FROM scheduling.Appointment WHERE SessionQuotaId = '{day.Id}';
        DELETE FROM scheduling.Reservation WHERE SessionQuotaId = '{day.Id}' OR BookingAttemptId IN (SELECT Id FROM @attempts);
        DELETE FROM scheduling.BookingAttempt WHERE Id IN (SELECT Id FROM @attempts);
        DELETE FROM scheduling.DoctorDayQuota WHERE Id = '{day.Id}';
        """);

    private sealed class FixedFeeBilling : IBillingModule
    {
        public Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct) =>
            Task.FromResult(new FeeQuote(Guid.NewGuid(), "TEST", ChargeType.ConsultationFee, PriceBasis.DoctorRank, null, 150_000m, "VND"));

        public Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> CreatePaymentAsync(CreatePayment command, CancellationToken ct) => throw new NotSupportedException();
    }

    // ---------- SQL ----------

    private static async Task<int> CountAsync(string sql)
    {
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
