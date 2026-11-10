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

namespace Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<SchedulingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SchedulingDbContext.Schema)))
            .AddScoped<IDoctorDayStore, SqlDoctorDayStore>()
            .AddScoped<IDoctorSearchQuotaReader, SqlDoctorSearchQuotaReader>()
            .AddScoped<IHoldStore, SqlHoldStore>()
            .AddScoped<IBookingAttemptStore, SqlBookingAttemptStore>()
            // Các service cần IUnitOfWork/IIdempotencyStore (Shared.Infrastructure) và IOperationTracker (Notifications.Infrastructure)
            // nên đăng ký cùng store, không ở AddSchedulingApplication (host test endpoint chỉ có Application).
            .AddScoped<HoldService>()
            .AddScoped<BookingAttemptService>()
            .AddScoped<BookingProgressService>()
            // PaymentVerifiedHandler (cần IBillingModule) đăng ký cùng dispatcher Outbox ở W3-00.
            .AddScoped<IConfirmationStore, SqlConfirmationStore>()
            // AppointmentChangeService (cần IBillingModule) đăng ký cùng endpoint đổi/hủy khi contract được duyệt.
            .AddScoped<IAppointmentChangeStore, SqlAppointmentChangeStore>();
}
