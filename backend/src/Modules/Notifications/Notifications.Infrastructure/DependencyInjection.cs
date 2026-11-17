using Notifications.Contracts;
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
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<WorkflowDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDbContext.Schema)))
            .AddScoped<IOperationTracker, EfOperationTracker>()
            .AddScoped<IPatientNotificationWriter, EfPatientNotificationWriter>()
            // Reader only runs in Api with Identity/Clinical; Worker only needs the writer and handlers.
            .AddScoped<IPatientNotificationReader>(provider => ActivatorUtilities.CreateInstance<EfPatientNotificationReader>(provider))
            .AddScoped<IIntegrationEventHandler<AppointmentConfirmed>, AppointmentNotificationHandler>()
            .AddScoped<IIntegrationEventHandler<AppointmentCancelled>, AppointmentNotificationHandler>();
}
