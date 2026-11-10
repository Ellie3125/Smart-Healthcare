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
    .AddSchedulingApplication();

var host = builder.Build();

// Hết hạn giữ chỗ (W2-02, Giải pháp §5.1). Hangfire chạy tối đa mỗi phút; lệnh xác nhận vẫn tự kiểm ExpiresAt.
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<HoldService>(
    "scheduling-expire-holds", service => service.ExpireDueAsync(CancellationToken.None), Cron.Minutely());

host.Run();
