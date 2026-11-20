using Shared.Infrastructure.Outbox;

namespace SmartHealthcare.Worker;

/// <summary>
/// Phát Outbox theo vòng lặp thay cho job Hangfire chạy theo phút: mỗi lượt chỉ là một câu claim trên index lọc,
/// không sinh bản ghi job. Lượt sau chỉ bắt đầu khi lượt trước xong nên không chạy chồng; lỗi một lượt chỉ ghi log.
/// </summary>
public sealed class OutboxDispatchLoop(Func<CancellationToken, Task> dispatchOnce, TimeSpan interval, ILogger<OutboxDispatchLoop> logger)
    : BackgroundService
{
    public OutboxDispatchLoop(IServiceScopeFactory scopes, OutboxOptions options, ILogger<OutboxDispatchLoop> logger)
        : this(async ct =>
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchPendingAsync(ct);
        }, TimeSpan.FromSeconds(options.PollIntervalSeconds), logger)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await dispatchOnce(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Lượt phát Outbox lỗi; thử lại sau {Interval}.", interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
