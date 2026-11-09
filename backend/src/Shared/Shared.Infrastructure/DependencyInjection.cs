using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SharedDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SharedDbContext.Schema)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OutboxWriter>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();
        services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IDbClock, DbClock>();
        services.AddScoped<AuditLogWriter>();
        return services;
    }

    /// <summary>Cho <typeparamref name="TContext"/> tham gia transaction của <see cref="IUnitOfWork"/> (dùng chung DbConnection/DbTransaction).</summary>
    public static IServiceCollection AddUnitOfWorkParticipant<TContext>(this IServiceCollection services) where TContext : DbContext =>
        services.AddScoped(sp => new UnitOfWorkParticipant(sp.GetRequiredService<TContext>()));
}
