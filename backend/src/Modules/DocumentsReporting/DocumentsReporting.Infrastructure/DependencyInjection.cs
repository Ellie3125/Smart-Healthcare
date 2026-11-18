using DocumentsReporting.Application;
using DocumentsReporting.Infrastructure.Adapters;
using DocumentsReporting.Infrastructure.Persistence;
using DocumentsReporting.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure;

namespace DocumentsReporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsReportingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        services.AddDbContext<DocumentsDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DocumentsDbContext.Schema)));

        services.AddUnitOfWorkParticipant<DocumentsDbContext>();

        services.AddScoped<IResultAttachmentStore, SqlResultAttachmentStore>();
        services.AddScoped<IAuditLogPort, AuditLogAdapter>();

        var r2Options = new R2Options();
        var section = configuration?.GetSection(R2Options.SectionName);
        if (section != null)
        {
            r2Options.AccountId = section["AccountId"];
            r2Options.AccessKeyId = section["AccessKeyId"];
            r2Options.SecretAccessKey = section["SecretAccessKey"];
            r2Options.BucketName = section["BucketName"];
            r2Options.ServiceUrl = section["ServiceUrl"];
        }
        services.TryAddSingleton(r2Options);

        if (r2Options.IsConfigured)
        {
            services.TryAddSingleton<HttpClient>();
            services.AddScoped<IObjectStorage, R2ObjectStorage>();
        }
        else
        {
            services.TryAddSingleton<IObjectStorage, InMemoryObjectStorage>();
        }

        return services;
    }
}
