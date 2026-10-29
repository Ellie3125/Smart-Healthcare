using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartHealthcare.Identity.Application;
using SmartHealthcare.Identity.Infrastructure.Persistence;
using SmartHealthcare.Identity.Infrastructure.Security;

namespace SmartHealthcare.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString, JwtTokenOptions jwt)
    {
        jwt.EnsureValid();
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<IdentityModuleDbContext>(o => IdentityModuleDbContext.Configure(o, connectionString));

        services.AddSingleton(jwt);
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();

        services.AddScoped<RegisterAccount>();
        services.AddScoped<LoginAccount>();
        services.AddScoped<GetMyAccount>();
        return services;
    }
}
