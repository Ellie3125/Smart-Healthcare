using System.Text;
using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Patients;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Application.Verifications;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Seed;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<IPatientProfileService, PatientProfileService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IRoomManagementService, RoomManagementService>();
        services.AddScoped<IStaffAdminService, StaffAdminService>();
        services.AddScoped<IdentityPatientsSeedService>();
        return services;
    }

    public static IServiceCollection AddIdentityPatientsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddIdentityPatientsPersistence(connectionString);
        services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();
        services.AddScoped<IPatientProfileService, PatientProfileService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IRoomManagementService, RoomManagementService>();
        services.AddScoped<IStaffAdminService, StaffAdminService>();
        services.AddScoped<IdentityPatientsSeedService>();
        return services;
    }

    public static IServiceCollection AddIdentityPatientsPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityPatientsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityPatientsDbContext.Schema);
                sql.CommandTimeout(600);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        return services;
    }

    public static IServiceCollection AddIdentityPatientsAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddIdentityCore<IdentityPatients.Domain.Entities.ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;

            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityPatients.Domain.Entities.ApplicationRole>()
        .AddEntityFrameworkStores<IdentityPatientsDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)] ?? string.Empty;
        var issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? "SmartHealthcare";
        var audience = jwtSection[nameof(JwtOptions.Audience)] ?? "SmartHealthcare";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
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
            options.AddPolicy("StaffOnly", policy =>
            {
                policy.RequireRole(Roles.Staff);
                policy.RequireClaim(AppClaims.Channel, Channels.Staff);
            });
            options.AddPolicy("ReceptionistOnly", policy =>
            {
                policy.RequireRole(Roles.Receptionist, Roles.Admin);
                policy.RequireClaim(AppClaims.Channel, Channels.Staff);
            });
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireRole(Roles.Admin);
                policy.RequireClaim(AppClaims.Channel, Channels.Staff);
            });
            options.AddPolicy("PatientOnly", policy =>
            {
                policy.RequireRole(Roles.Patient);
                policy.RequireClaim(AppClaims.Channel, Channels.Patient);
            });
        });

        return services;
    }
}
