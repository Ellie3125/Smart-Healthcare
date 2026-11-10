using Billing.Contracts;
using Billing.Infrastructure;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.Holds;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure;
using Shared.Infrastructure.Outbox;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SmartHealthcare");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:SmartHealthcare chưa được cấu hình (dotnet user-secrets hoặc biến môi trường ConnectionStrings__SmartHealthcare).");
}

// Hangfire tự tạo và quản lý schema hangfire; EF không tạo bảng này.
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true,
        SqlClientFactory = SqlClientFactory.Instance,
    }));
builder.Services.AddHangfireServer();

builder.Services
    .AddSharedInfrastructure(connectionString)
    .AddSchedulingInfrastructure(connectionString)
    .AddNotificationsInfrastructure(connectionString)
    .AddUnitOfWorkParticipant<SchedulingDbContext>()
    .AddUnitOfWorkParticipant<WorkflowDbContext>()
    .AddSchedulingApplication()
    // PaymentVerifiedHandler báo giá lại phí khám qua IBillingModule (W2-06).
    .AddBillingInfrastructure(connectionString)
    .AddOutboxDispatcher(builder.Configuration.GetSection(OutboxOptions.Section).Get<OutboxOptions>() ?? new OutboxOptions())
    .AddOutboxEvent<PaymentVerified>();

var host = builder.Build();

// Hết hạn giữ chỗ (W2-02, Giải pháp §5.1). Hangfire chạy tối đa mỗi phút; lệnh xác nhận vẫn tự kiểm ExpiresAt.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<HoldService>(
    "scheduling-expire-holds", service => service.ExpireDueAsync(CancellationToken.None), Cron.Minutely());

// Phát Outbox tới handler (W3-00, Giải pháp §8.3). Mỗi lần chạy xử lý tới khi hết bản ghi tới hạn; nhiều worker không nhận trùng.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxDispatcher>(
    "shared-outbox-dispatch", dispatcher => dispatcher.DispatchPendingAsync(CancellationToken.None),
    builder.Configuration[$"{OutboxOptions.Section}:Cron"] ?? Cron.Minutely());

host.Run();
