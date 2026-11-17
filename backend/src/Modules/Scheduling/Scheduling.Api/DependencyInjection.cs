using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;

namespace Scheduling.Api;

public static class DependencyInjection
{
    public const string Tag = "Scheduling";

    public static IServiceCollection AddSchedulingApi(this IServiceCollection services) =>
        services.AddSingleton<IDoctorDirectory, DevelopmentDoctorDirectory>()
            .AddSingleton<IDoctorSearchDirectory, DevelopmentDoctorSearchDirectory>()
            .AddScoped(provider => new DoctorDayPolicy(provider.GetRequiredService<IConfiguration>()
                .GetValue("Scheduling:DoctorDays:CheckInDeadline", new TimeOnly(11, 0))));

    public static RouteGroupBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scheduling").WithTags(Tag);
        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            group.MapDoctorDayEndpoints();
            group.MapDoctorSearchEndpoints();
            group.MapBookingProgressEndpoints();
            group.MapPatientAppointmentEndpoints();
        }

        return group;
    }
}

internal sealed class DevelopmentDoctorDirectory(IConfiguration configuration) : IDoctorDirectory
{
    public Task<Guid?> GetAssignedRoomIdAsync(Guid doctorId, CancellationToken ct) =>
        Task.FromResult<Guid?>(Guid.TryParse(configuration[$"Scheduling:DoctorDays:DevelopmentDoctorRooms:{doctorId:D}"],
            out var roomId) ? roomId : null);
}

internal sealed class DevelopmentDoctorSearchDirectory(IConfiguration configuration) : IDoctorSearchDirectory
{
    public Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DoctorSearchProfile>>(
            configuration.GetSection("Scheduling:DoctorSearch:DevelopmentDoctors").Get<DoctorSearchProfile[]>() ?? []);
}
