using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.Holds;
using Scheduling.Infrastructure.BookingAttempts;
using Scheduling.Infrastructure.DoctorDays;
using Scheduling.Infrastructure.Holds;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<SchedulingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SchedulingDbContext.Schema)))
            .AddScoped<IDoctorDayStore, SqlDoctorDayStore>()
            .AddScoped<IHoldStore, SqlHoldStore>()
            .AddScoped<IBookingAttemptStore, SqlBookingAttemptStore>()
            // Hai service cần IUnitOfWork/IIdempotencyStore (Shared.Infrastructure) và IOperationTracker (Notifications.Infrastructure)
            // nên đăng ký cùng store, không ở AddSchedulingApplication (host test endpoint chỉ có Application).
            .AddScoped<HoldService>()
            .AddScoped<BookingAttemptService>();
}
