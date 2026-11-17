using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.Appointments;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Scheduling.Application.WalkIn;

namespace Scheduling.Api;

public static class DependencyInjection
{
    public const string Tag = "Scheduling";

    public static IServiceCollection AddSchedulingApi(this IServiceCollection services) =>
        services.AddScoped<IDoctorDirectory, IdentityDoctorDirectory>()
            .AddScoped<IDoctorSearchDirectory, IdentityDoctorSearchDirectory>()
            .AddScoped<IWalkInPatientDirectory, IdentityWalkInPatientDirectory>()
            .AddScoped<DoctorDayListQuery>()
            // Factory: host chỉ dùng route đọc (test) không cần đủ phụ thuộc Billing/Outbox của lệnh đổi/hủy.
            .AddScoped(provider => ActivatorUtilities.CreateInstance<AppointmentChangeService>(provider))
            .AddScoped(provider => new DoctorDayPolicy(provider.GetRequiredService<IConfiguration>()
                .GetValue("Scheduling:DoctorDays:CheckInDeadline", new TimeOnly(11, 0))));

    public static RouteGroupBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scheduling").WithTags(Tag);
        group.MapMedicalServiceCatalogRoutes();
        group.MapSessionAdministrationRoutes();
        group.MapDoctorDayEndpoints();
        group.MapDoctorSearchEndpoints();
        group.MapBookingProgressEndpoints();
        group.MapPatientAppointmentEndpoints();
        group.MapRoomQueueRoutes();
        group.MapQueueEndpoints();
        return group;
    }
}
