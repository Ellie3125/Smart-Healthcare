using Billing.Contracts;
using Billing.Infrastructure;
using Clinical.Infrastructure;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Notifications.Api;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Contracts;
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
            // Job cuối ngày CLS và kết thúc phiên đọc trạng thái Encounter qua IClinicalModule.
            .AddClinicalInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<Clinical.Infrastructure.Persistence.ClinicalDbContext>()
            .AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddUnitOfWorkParticipant<WorkflowDbContext>()
            // Handler PaymentVerified của Scheduling gọi IPaymentRefunds (PaidUnallocated) trong cùng transaction.
            .AddUnitOfWorkParticipant<Billing.Infrastructure.Persistence.BillingDbContext>()
            // Không gọi AddSchedulingApplication: các service đó phục vụ HTTP, cần danh bạ bác sĩ và Identity mà Worker không có.
            // PaymentVerifiedHandler báo giá lại phí khám qua IBillingModule (W2-06).
            .AddBillingInfrastructure(connectionString)
            .AddOutboxDispatcher(configuration.GetSection(OutboxOptions.Section).Get<OutboxOptions>() ?? new OutboxOptions())
            .AddOutboxEvent<PaymentVerified>()
            // Billing gắn phí khám đã thu với lịch hẹn (ExamChargeLinker). Module khác nhận sự kiện này thì chỉ đăng ký handler,
            // không gọi AddOutboxEvent lần nữa.
            .AddOutboxEvent<AppointmentConfirmed>()
            // Billing tạo yêu cầu hoàn phí khám khi hủy lịch được hoàn (RefundService); module khác chỉ đăng ký handler.
            .AddOutboxEvent<AppointmentCancelled>()
            // Check-in phát TicketCheckedIn; Clinical không có handler (Q1), dispatcher vẫn phải nhận để bản ghi không kẹt Pending.
            .AddOutboxEvent<TicketCheckedIn>()
            // Gọi số (W4-02): handler Notifications đẩy QueueUpdated/DisplayBoardUpdated qua SignalR.
            .AddOutboxEvent<QueueCalled>()
            // Vé CLS bị hủy cuối ngày (W3-13); Billing chưa có handler, dispatcher nhận để bản ghi không kẹt Pending.
            .AddOutboxEvent<ClsTicketCancelled>()
            // Handler trong Worker đẩy sự kiện SignalR qua Redis tới kết nối đang mở ở BE1/BE2 (W4-01).
            .AddNotificationsApi()
            .AddNotificationsRedisBackplane(configuration.GetConnectionString("Redis"));
    }
}
