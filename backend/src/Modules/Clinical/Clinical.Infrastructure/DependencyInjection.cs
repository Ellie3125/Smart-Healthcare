using Clinical.Application;
using Clinical.Contracts;
using Clinical.Infrastructure.Persistence;
using Clinical.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinical.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClinicalDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ClinicalDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEncounterStore, EfEncounterStore>();
        services.AddScoped<Clinical.Application.StaffLists.IClinicalStaffListReader, EfClinicalStaffListReader>();
        services.AddScoped<Clinical.Application.Reports.IClinicalReportReader, Reports.EfClinicalReportReader>();
        services.AddScoped<IServiceOrderStore, EfServiceOrderStore>();
        services.AddScoped<IPrescriptionStore, EfPrescriptionStore>();
        services.AddScoped<Clinical.Application.Vitals.IVitalsStore, EfVitalsStore>();
        services.AddScoped<Clinical.Application.Vitals.RecordVitalsService>();
        services.AddScoped<Clinical.Application.Orders.IClsOrderStore, EfClsOrderStore>();
        services.AddScoped<Clinical.Application.Records.IClinicalRecordStore, EfClinicalRecordStore>();
        services.AddScoped<Clinical.Application.Records.ClinicalRecordService>();
        services.AddScoped<Clinical.Application.Results.IClsLifecycleStore, EfClsLifecycleStore>();
        services.AddScoped<Clinical.Application.Results.IClsResultStore, EfClsResultStore>();
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<Clinical.Application.Results.ClsLifecycleService>(sp));
        services.AddScoped<IClinicalModule>(sp => new ClinicalModule(sp.GetRequiredService<IEncounterStore>(),
            sp.GetRequiredService<IServiceOrderStore>(), sp.GetRequiredService<IPrescriptionStore>(), sp.GetRequiredService<TimeProvider>(),
            () => sp.GetRequiredService<Clinical.Application.Results.ClsLifecycleService>(),
            () => sp.GetRequiredService<Clinical.Application.Encounters.EncounterProgressService>(),
            sp.GetRequiredService<Clinical.Application.Reports.IClinicalReportReader>()));
        services.AddScoped<Clinical.Application.Encounters.IEncounterProgressStore, EfEncounterProgressStore>();
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<Clinical.Application.Encounters.EncounterProgressService>(sp));

        return services;
    }
}
