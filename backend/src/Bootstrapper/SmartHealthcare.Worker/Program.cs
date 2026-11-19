using Billing.Application;
using Hangfire;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Holds;
using Shared.Infrastructure;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence;
using SmartHealthcare.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Máy dev: `dotnet run` dùng DB trên máy, `dotnet run -lp docker` dùng DB trong Docker Compose (xem DatabaseSource).
var connectionName = DatabaseSource.ConnectionName(builder.Configuration[DatabaseSource.ConfigurationKey]);
var connectionString = builder.Configuration.GetConnectionString(connectionName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException($"ConnectionStrings:{connectionName} chưa được cấu hình (dotnet user-secrets hoặc biến môi trường ConnectionStrings__{connectionName}).");
}

builder.Services.AddWorkerServices(builder.Configuration, connectionString);

var host = builder.Build();

// Hết hạn giữ chỗ (W2-02, Giải pháp §5.1). Hangfire chạy tối đa mỗi phút; lệnh xác nhận vẫn tự kiểm ExpiresAt.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<HoldService>(
    "scheduling-expire-holds", service => service.ExpireDueAsync(CancellationToken.None), Cron.Minutely());

// Quá hạn check-in (W3-02): lịch Scheduled của phiên đã qua CheckInDeadline chuyển Absent; giữ quota và phí.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<CheckInDeadlineJob>(
    "scheduling-check-in-deadline", job => job.RunAsync(CancellationToken.None), Cron.Minutely());

// Cuối ngày khám D (Giải pháp §5.5): hủy khoản phí CLS chưa trả của các ngày đã qua. 17:05 UTC = 00:05 giờ Việt Nam.
// Worker tắt đúng lúc đó thì Hangfire chạy bù một lần khi khởi động lại; job tự tính mốc theo giờ hiện tại nên chạy bù vẫn đúng.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ClsDayEndService>(
    "billing-cls-day-end", service => service.CancelUnpaidAsync(CancellationToken.None), "5 17 * * *");

// Đối soát VNPAY (Giải pháp §5.3): yêu cầu quá hạn 2 phút mà chưa có IPN thì hỏi querydr; đã thu thì ghi nhận như IPN,
// cổng không có giao dịch thì chuyển Expired, chưa rõ quá 30 phút thì chuyển Unknown cho người xử lý.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PaymentReconciliationService>(
    "billing-reconcile-vnpay", service => service.ReconcileOverdueAsync(CancellationToken.None), "*/5 * * * *");

// Vé CLS chưa làm của ngày đã qua chuyển Cancelled (W3-13). 17:05 UTC = 00:05 giờ Việt Nam; job tự tính mốc nên chạy bù vẫn đúng.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<Scheduling.Infrastructure.Queues.ClsDayEndJob>(
    "scheduling-cls-day-end", job => job.RunAsync(CancellationToken.None), "5 17 * * *");

// Kết thúc phiên khám theo trạng thái cuối của lịch hẹn (W3-07); không tự đóng theo giờ.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<Scheduling.Infrastructure.Sessions.SessionCompletionJob>(
    "scheduling-session-completion", job => job.RunAsync(CancellationToken.None), "*/5 * * * *");

// Outbox nay do OutboxDispatchLoop phát theo Outbox:PollIntervalSeconds; xóa job theo phút cũ còn lưu trong storage Hangfire.
host.Services.GetRequiredService<IRecurringJobManager>().RemoveIfExists("shared-outbox-dispatch");

// Dọn dữ liệu kỹ thuật quá 30 ngày (Outbox Published, ProcessedEvent, idempotency hết hạn). 19:00 UTC = 02:00 giờ Việt Nam.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TechnicalDataCleanupJob>(
    "shared-technical-data-cleanup", job => job.RunAsync(CancellationToken.None), "0 19 * * *");

host.Run();
