using System.Text;
using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Patients;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Application.Verifications;
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

        // Mỗi AppUser một Role; AuthService chỉ cấp token kênh Staff cho vai trò nhân viên và kênh Patient cho Patient,
        // nên kiểm vai trò đã đủ phân kênh. Token vẫn mang claim channel.
        services.AddAuthorization(options =>
        {
            options.AddPolicy("StaffOnly", policy => policy.RequireRole(Roles.Staff));
            options.AddPolicy("ReceptionistOnly", policy => policy.RequireRole(Roles.Receptionist, Roles.Admin));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy("PatientOnly", policy => policy.RequireRole(Roles.Patient));
        });

        return services;
    }
}
