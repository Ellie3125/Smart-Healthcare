using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Seed;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPatients.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityPatientsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddIdentityPatientsPersistence(connectionString);
        services.AddIdentityPatientsAuth(configuration);
        services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();
        services.AddScoped<IdentityPatientsSeedService>();
        return services;
    }

    public static IServiceCollection AddIdentityPatientsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddIdentityPatientsPersistence(connectionString);
        services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();
        services.AddScoped<IdentityPatientsSeedService>();
        return services;
    }

    public static IServiceCollection AddIdentityPatientsPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityPatientsDbContext>(options =>
            options.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityPatientsDbContext.Schema)));

        return services;
    }

    public static IServiceCollection AddIdentityPatientsAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)] ?? string.Empty;
        var issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? "SmartHealthcare";
        var audience = jwtSection[nameof(JwtOptions.Audience)] ?? "SmartHealthcare";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(string.IsNullOrEmpty(signingKey) ? "DefaultDevSigningKeyWithAtLeast32BytesForHmacSha256!" : signingKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("StaffOnly", policy => policy.RequireRole(Roles.Staff));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
        });

        return services;
    }
}
