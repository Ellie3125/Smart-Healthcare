using Billing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.Appointments;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Confirmations;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Scheduling.Application.Holds;
using Scheduling.Infrastructure.Appointments;
using Scheduling.Infrastructure.BookingAttempts;
using Scheduling.Infrastructure.Confirmations;
using Scheduling.Infrastructure.DoctorDays;
using Scheduling.Infrastructure.DoctorSearch;
using Scheduling.Infrastructure.Holds;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Application.CheckIn;
using Scheduling.Infrastructure.CheckIn;
using Scheduling.Application.WalkIn;
using Scheduling.Infrastructure.WalkIn;
using Scheduling.Application.RoomQueues;
using Scheduling.Infrastructure.RoomQueues;
using Shared.Abstractions;

namespace Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<SchedulingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SchedulingDbContext.Schema)))
            // Catalog chỉ được resolve ở Api có Identity; Worker cũng dùng AddSchedulingInfrastructure.
            .AddScoped<Scheduling.Application.MedicalServiceAdmin.IMedicalServiceCatalog>(provider =>
                ActivatorUtilities.CreateInstance<Scheduling.Infrastructure.MedicalServiceAdmin.SqlMedicalServiceCatalog>(provider))
            .AddScoped<Scheduling.Application.SessionAdministration.ISessionAdministration>(provider =>
                ActivatorUtilities.CreateInstance<Scheduling.Infrastructure.SessionAdministration.SqlSessionAdministration>(provider))
            .AddScoped<IDoctorDayStore, SqlDoctorDayStore>()
            .AddScoped<IDoctorSearchQuotaReader, SqlDoctorSearchQuotaReader>()
            .AddScoped<IHoldStore, SqlHoldStore>()
            .AddScoped<IBookingAttemptStore, SqlBookingAttemptStore>()
            .AddScoped<IBookingPaymentStore, SqlBookingPaymentStore>()
            .AddScoped<IPatientAppointmentReader, SqlPatientAppointmentReader>()
            .AddScoped<ICheckInStore, SqlCheckInStore>()
            .AddScoped<CheckInDeadlineJob>()
            .AddScoped<IWalkInStore, SqlWalkInStore>()
            .AddScoped<IRoomQueueReader, SqlRoomQueueReader>()
            .AddScoped<IClinicalQueueReader, SqlClinicalQueueReader>()
            .AddScoped<Scheduling.Application.Reports.ISchedulingReportReader, Reports.SqlSchedulingReportReader>()
            .AddScoped<Scheduling.Application.Queues.IQueueStore, Queues.SqlQueueStore>()
            .AddScoped<Scheduling.Application.Queues.RoomAuditRequest>()
            .AddScoped<Scheduling.Application.Queues.IClsTicketStore, Queues.SqlClsTicketStore>()
            .AddScoped<Scheduling.Application.Queues.IClsPendingResultReader>(provider => ActivatorUtilities.CreateInstance<Queues.SqlClsPendingResultReader>(provider))
            .AddScoped<Scheduling.Contracts.IClsServiceDirectory, Queues.SqlClsServiceDirectory>()
            .AddScoped<Scheduling.Contracts.IClsTicketReader, Queues.SqlClsTicketReader>()
            .AddScoped(provider => ActivatorUtilities.CreateInstance<Queues.ClsDayEndJob>(provider))
            .AddScoped<Scheduling.Contracts.ICoordinationExceptionReader>(provider => ActivatorUtilities.CreateInstance<Coordination.CoordinationExceptionReader>(provider))
            .AddScoped(provider => ActivatorUtilities.CreateInstance<Sessions.SessionCompletionJob>(provider))
            .AddScoped<IIntegrationEventHandler<Clinical.Contracts.ResultPublished>>(provider =>
                ActivatorUtilities.CreateInstance<Queues.ReturnTicketActivationHandler>(provider))
            // Các service cần IUnitOfWork/IIdempotencyStore (Shared.Infrastructure) và IOperationTracker (Notifications.Infrastructure)
            // nên đăng ký cùng store, không ở AddSchedulingApplication (host test endpoint chỉ có Application).
            .AddScoped<HoldService>()
            .AddScoped<BookingAttemptService>()
            // Cần IBillingModule; Api và Worker đều có (W2-06).
            .AddScoped<BookingPaymentService>()
            // BookingProgressService cần IIdentityPatientsModule nên đăng ký ở host có Identity (SmartHealthcare.Api), không ở đây: Worker cũng gọi hàm này.
            // Dispatcher Outbox (W3-00) gọi handler; host cần IBillingModule khi resolve.
            .AddScoped<IIntegrationEventHandler<PaymentVerified>, PaymentVerifiedHandler>()
            .AddScoped<IConfirmationStore, SqlConfirmationStore>()
            // AppointmentChangeService (cần IBillingModule) đăng ký ở Scheduling.Api cùng route đổi/hủy.
            .AddScoped<IAppointmentChangeStore, SqlAppointmentChangeStore>();
}
