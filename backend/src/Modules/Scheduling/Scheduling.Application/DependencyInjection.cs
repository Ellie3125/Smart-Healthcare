using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Scheduling.Contracts;

namespace Scheduling.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingApplication(this IServiceCollection services) =>
        services.AddScoped<DoctorDayService>().AddScoped<DoctorSearchService>()
            .AddScoped<ISchedulingModule, SchedulingModule>();
}
