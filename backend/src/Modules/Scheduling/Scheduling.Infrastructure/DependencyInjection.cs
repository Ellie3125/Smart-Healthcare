using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.DoctorDays;
using Scheduling.Infrastructure.DoctorDays;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<SchedulingDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SchedulingDbContext.Schema)))
            .AddScoped<IDoctorDayStore, SqlDoctorDayStore>();
}
