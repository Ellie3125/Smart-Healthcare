using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Chat;
using Notifications.Contracts;
using Notifications.Infrastructure.Chat;
using Notifications.Infrastructure.Operations;
using Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.PatientNotifications;
using Notifications.Infrastructure.PatientNotifications;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Chuyển tiếp chat tới chatbot-service (contract chatbot §2, §3). Thiếu cấu hình thì chatbot coi như tạm ngưng.
        services.AddOptions<ChatbotOptions>().BindConfiguration(ChatbotOptions.Section);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ChatbotHealthCache>();
        // Một lượt có thể gọi model trên Colab: chờ tối đa 90 giây rồi trả ChatbotUnavailable.
        services.AddHttpClient<IChatbotGateway, HttpChatbotGateway>(client => client.Timeout = TimeSpan.FromSeconds(90));
        return services.AddDbContext<WorkflowDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDbContext.Schema)))
            .AddScoped<IOperationTracker, EfOperationTracker>()
            .AddScoped<Notifications.Application.Exceptions.IWorkflowExceptionMonitor>(provider =>
                ActivatorUtilities.CreateInstance<Notifications.Infrastructure.Exceptions.WorkflowExceptionMonitor>(provider))
            .AddScoped<IPatientNotificationWriter, EfPatientNotificationWriter>()
            // Reader only runs in Api with Identity/Clinical; Worker only needs the writer and handlers.
            .AddScoped<IPatientNotificationReader>(provider => ActivatorUtilities.CreateInstance<EfPatientNotificationReader>(provider))
            .AddScoped<IIntegrationEventHandler<AppointmentConfirmed>, AppointmentNotificationHandler>()
            .AddScoped<IIntegrationEventHandler<AppointmentCancelled>, AppointmentNotificationHandler>()
            .AddScoped<Shared.Abstractions.IIntegrationEventHandler<Scheduling.Contracts.QueueCalled>>(provider =>
                ActivatorUtilities.CreateInstance<QueueCalledRealtimeHandler>(provider));
    }
}
