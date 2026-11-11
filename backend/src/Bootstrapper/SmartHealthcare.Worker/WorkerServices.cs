using Billing.Contracts;
using Billing.Infrastructure;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure;
using Shared.Infrastructure.Outbox;

namespace SmartHealthcare.Worker;

public static class WorkerServices
{
    /// <summary>Toàn bộ đăng ký DI của Worker; tách khỏi Program.cs để test dựng host kiểm được (ArchitectureTests).</summary>
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        // Hangfire tự tạo và quản lý schema hangfire; EF không tạo bảng này.
        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
                SqlClientFactory = SqlClientFactory.Instance,
            }));
        services.AddHangfireServer();

        return services
            .AddSharedInfrastructure(connectionString)
            .AddSchedulingInfrastructure(connectionString)
            .AddNotificationsInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            // Không gọi AddSchedulingApplication: các service đó phục vụ HTTP, cần danh bạ bác sĩ và Identity mà Worker không có.
            // PaymentVerifiedHandler báo giá lại phí khám qua IBillingModule (W2-06).
            .AddBillingInfrastructure(connectionString)
            .AddOutboxDispatcher(configuration.GetSection(OutboxOptions.Section).Get<OutboxOptions>() ?? new OutboxOptions())
            .AddOutboxEvent<PaymentVerified>();
    }
}
