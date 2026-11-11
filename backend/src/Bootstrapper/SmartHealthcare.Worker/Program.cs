using Hangfire;
using Scheduling.Application.Holds;
using Shared.Infrastructure.Outbox;
using SmartHealthcare.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SmartHealthcare");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:SmartHealthcare chưa được cấu hình (dotnet user-secrets hoặc biến môi trường ConnectionStrings__SmartHealthcare).");
}

builder.Services.AddWorkerServices(builder.Configuration, connectionString);

var host = builder.Build();

// Hết hạn giữ chỗ (W2-02, Giải pháp §5.1). Hangfire chạy tối đa mỗi phút; lệnh xác nhận vẫn tự kiểm ExpiresAt.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<HoldService>(
    "scheduling-expire-holds", service => service.ExpireDueAsync(CancellationToken.None), Cron.Minutely());

// Phát Outbox tới handler (W3-00, Giải pháp §8.3). Mỗi lần chạy xử lý tới khi hết bản ghi tới hạn; nhiều worker không nhận trùng.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxDispatcher>(
    "shared-outbox-dispatch", dispatcher => dispatcher.DispatchPendingAsync(CancellationToken.None),
    builder.Configuration[$"{OutboxOptions.Section}:Cron"] ?? Cron.Minutely());

host.Run();
