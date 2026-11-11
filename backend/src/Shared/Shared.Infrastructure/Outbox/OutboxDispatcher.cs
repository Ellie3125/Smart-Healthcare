using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Outbox;

/// <summary>Giới hạn của dispatcher Outbox; Worker đọc từ mục "Outbox" của appsettings.</summary>
public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    public int BatchSize { get; set; } = 20;

    /// <summary>Số lần thử tối đa (kể cả lần đầu); lần thử thất bại thứ MaxAttempts chuyển DeadLetter.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Lần thử lại thứ n chờ RetryDelaySeconds × 2^(n−1) giây, tính theo giờ DB.</summary>
    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>Bản ghi Processing quá lâu (worker chết giữa chừng) được nhận lại và tính là một lần thử.</summary>
    public int LeaseSeconds { get; set; } = 300;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1, nameof(BatchSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1, nameof(MaxAttempts));
        ArgumentOutOfRangeException.ThrowIfNegative(RetryDelaySeconds, nameof(RetryDelaySeconds));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(RetryDelaySeconds, 86_400, nameof(RetryDelaySeconds));
        ArgumentOutOfRangeException.ThrowIfLessThan(LeaseSeconds, 1, nameof(LeaseSeconds));
    }
}

/// <summary>Cách giải payload và gọi các handler của một loại sự kiện; đăng ký bằng <c>AddOutboxEvent&lt;TEvent&gt;()</c>.</summary>
public sealed record OutboxEventRegistration(EventType EventType,
    Func<IServiceProvider, string, CancellationToken, Task> DispatchAsync);

/// <summary>
/// Phát shared.OutboxMessage tới <see cref="IIntegrationEventHandler{TEvent}"/> (Giải pháp v2.14 §8.2–8.3, MSG-02, MSG-05).
/// Nhận bản ghi bằng một câu UPDATE có điều kiện (UPDLOCK + READPAST) nên nhiều worker không nhận trùng.
/// Chống lặp là việc của handler (ProcessedEvent theo EventId + HandlerName, cùng transaction với tác dụng).
/// Lỗi: RetryCount + 1 và thử lại sau backoff; đủ <see cref="OutboxOptions.MaxAttempts"/> thì DeadLetter để xử lý tay.
/// ProcessedAtUtc giữ thời điểm lần xử lý gần nhất (nhận, lỗi hoặc Published), dùng cho backoff và lease; ERD v2.4 không thêm cột.
/// Chỉ nhận loại sự kiện đã đăng ký; loại chưa có handler giữ Pending cho tới khi có.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IEnumerable<OutboxEventRegistration> registrations,
    OutboxOptions options, ILogger<OutboxDispatcher> logger)
{
    private readonly Dictionary<string, OutboxEventRegistration> _registrations =
        registrations.ToDictionary(x => x.EventType.ToString());

    /// <summary>Nhận và xử lý từng lô tới khi không còn bản ghi tới hạn. Trả về số lần xử lý.</summary>
    public async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        if (_registrations.Count == 0)
            return 0;

        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = await ClaimAsync(ct);
            if (batch.Count == 0)
                break;
            foreach (var message in batch)
                await DispatchAsync(message, ct);
            total += batch.Count;
        }

        return total;
    }

    private async Task<List<ClaimedMessage>> ClaimAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        var now = await scope.ServiceProvider.GetRequiredService<IDbClock>().GetUtcNowAsync(ct);
        // Chèn thẳng tên EventType vào SQL: lấy từ enum đã đăng ký, không từ dữ liệu ngoài.
        var types = string.Join(", ", _registrations.Keys.Select(x => $"N'{x}'"));

        // shortcut: chưa có index theo Status nên quét bảng; thêm index có lọc (Status Pending/Processing) khi Outbox lớn.
        var sql = $"""
            WITH due AS (
                SELECT TOP (@batch) Id, EventType, PayloadJson, RetryCount, Status, ProcessedAtUtc
                FROM shared.OutboxMessage WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE EventType IN ({types})
                  AND ((Status = N'Pending' AND (RetryCount = 0 OR ProcessedAtUtc IS NULL
                        OR DATEADD(SECOND, @delay * POWER(2, IIF(RetryCount > 10, 10, RetryCount) - 1), ProcessedAtUtc) <= @now))
                    OR (Status = N'Processing' AND ProcessedAtUtc <= DATEADD(SECOND, -@lease, @now)))
                ORDER BY CreatedAtUtc, Id)
            UPDATE due
            SET RetryCount = IIF(Status = N'Processing', RetryCount + 1, RetryCount), Status = N'Processing', ProcessedAtUtc = @now
            OUTPUT inserted.Id, inserted.EventType, inserted.PayloadJson, inserted.RetryCount;
            """;
        return await shared.Database.SqlQueryRaw<ClaimedMessage>(sql,
            new SqlParameter("@batch", options.BatchSize),
            new SqlParameter("@delay", options.RetryDelaySeconds),
            new SqlParameter("@lease", options.LeaseSeconds),
            new SqlParameter("@now", System.Data.SqlDbType.DateTime2) { Value = now })
            .ToListAsync(ct);
    }

    private async Task DispatchAsync(ClaimedMessage message, CancellationToken ct)
    {
        // Nhận lại sau lease khi đã đủ số lần thử.
        if (message.RetryCount >= options.MaxAttempts)
        {
            logger.LogError("Outbox {EventId} ({EventType}) chuyển DeadLetter sau {Attempts} lần thử.",
                message.Id, message.EventType, message.RetryCount);
            await MarkAsync(message, OutboxMessageStatus.DeadLetter, message.RetryCount, ct);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await _registrations[message.EventType].DispatchAsync(scope.ServiceProvider, message.PayloadJson, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var attempts = message.RetryCount + 1;
            var dead = attempts >= options.MaxAttempts;
            logger.Log(dead ? LogLevel.Error : LogLevel.Warning, ex,
                "Outbox {EventId} ({EventType}) lỗi lần {Attempt}/{MaxAttempts}; DeadLetter: {DeadLetter}.",
                message.Id, message.EventType, attempts, options.MaxAttempts, dead);
            await MarkAsync(message, dead ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Pending, attempts, ct);
            return;
        }

        await MarkAsync(message, OutboxMessageStatus.Published, message.RetryCount, ct);
    }

    /// <summary>Chỉ cập nhật khi bản ghi vẫn do lần nhận này giữ (Processing, cùng RetryCount); đã bị nhận lại sau lease thì bỏ qua.</summary>
    private async Task MarkAsync(ClaimedMessage message, OutboxMessageStatus status, int retryCount, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        var now = await scope.ServiceProvider.GetRequiredService<IDbClock>().GetUtcNowAsync(ct);
        var statusName = status.ToString();
        await shared.Database.ExecuteSqlAsync($"""
            UPDATE shared.OutboxMessage SET Status = {statusName}, RetryCount = {retryCount}, ProcessedAtUtc = {now}
            WHERE Id = {message.Id} AND Status = N'Processing' AND RetryCount = {message.RetryCount}
            """, ct);
    }

    private sealed class ClaimedMessage
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = null!;
        public string PayloadJson { get; set; } = null!;
        public int RetryCount { get; set; }
    }
}

public static class OutboxDispatcherRegistration
{
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        services.AddLogging();
        services.AddSingleton(options);
        services.AddScoped<OutboxDispatcher>();
        return services;
    }

    /// <summary>Cho dispatcher phát <typeparamref name="TEvent"/> (EventType theo tên lớp) tới mọi handler đã đăng ký.</summary>
    public static IServiceCollection AddOutboxEvent<TEvent>(this IServiceCollection services) where TEvent : IIntegrationEvent
    {
        if (!Enum.TryParse<EventType>(typeof(TEvent).Name, out var type))
            throw new ArgumentException($"Sự kiện {typeof(TEvent).Name} không có trong danh sách EventType của Outbox.");

        return services.AddSingleton(new OutboxEventRegistration(type, async (provider, json, ct) =>
        {
            var e = JsonSerializer.Deserialize<TEvent>(json, OutboxWriter.JsonOptions)
                ?? throw new JsonException($"Payload {type} rỗng.");
            foreach (var handler in provider.GetServices<IIntegrationEventHandler<TEvent>>())
                await handler.HandleAsync(e, ct);
        }));
    }
}
