using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.DoctorDays;

namespace Scheduling.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingApplication(this IServiceCollection services) =>
        services.AddScoped<DoctorDayService>();
}
