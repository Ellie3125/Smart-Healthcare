using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartHealthcare.Patients.Application;
using SmartHealthcare.Patients.Infrastructure.Persistence;

namespace SmartHealthcare.Patients.Infrastructure;

public static class PatientsModule
{
    public static IServiceCollection AddPatientsModule(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<PatientsModuleDbContext>(o => PatientsModuleDbContext.Configure(o, connectionString));
        services.AddScoped<IPatientProfileRepository, PatientProfileRepository>();

        services.AddScoped<CreatePatientProfile>();
        services.AddScoped<ListMyPatientProfiles>();
        services.AddScoped<GetPatientProfile>();
        services.AddScoped<UpdatePatientProfile>();
        services.AddScoped<RevokeMyProfileLink>();
        return services;
    }
}
