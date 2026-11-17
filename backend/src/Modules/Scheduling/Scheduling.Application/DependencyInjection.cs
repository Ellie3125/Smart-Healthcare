using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Queues;
using Scheduling.Application.WalkIn;
using Scheduling.Contracts;

namespace Scheduling.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingApplication(this IServiceCollection services) =>
        services.AddScoped<DoctorDayService>().AddScoped<DoctorSearchService>()
            // Host chỉ dùng tìm bác sĩ không cần store/Clinical của check-in (ví dụ test policy Identity).
            .AddScoped(provider => ActivatorUtilities.CreateInstance<CheckInService>(provider))
            .AddScoped(provider => ActivatorUtilities.CreateInstance<WalkInService>(provider))
            .AddSingleton(QueuePolicy.Default)
            .AddScoped(provider => ActivatorUtilities.CreateInstance<QueueService>(provider))
            .AddScoped(provider => ActivatorUtilities.CreateInstance<ClsTicketService>(provider))
            .AddScoped<ISchedulingModule>(provider => new SchedulingModule(provider.GetRequiredService<DoctorSearchService>(),
                () => provider.GetRequiredService<CheckInService>(), () => provider.GetRequiredService<ClsTicketService>()));
}
