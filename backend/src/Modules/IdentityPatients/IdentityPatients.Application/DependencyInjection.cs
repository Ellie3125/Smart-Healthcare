using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Services;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityPatientsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();

        return services;
    }
}
