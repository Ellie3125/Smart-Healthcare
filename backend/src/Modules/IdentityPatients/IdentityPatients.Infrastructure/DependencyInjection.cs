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
                // Không bật EnableRetryOnFailure: DbContext tham gia IUnitOfWork, UoW tự chạy lại khi deadlock 1205 (D7).
                sql.CommandTimeout(600);
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
        // Khóa ký chỉ lấy từ user-secrets hoặc biến môi trường; thiếu hoặc ngắn thì app không khởi động (D9).
        if (Encoding.UTF8.GetByteCount(signingKey) < JwtOptions.MinSigningKeyBytes)
        {
            throw new InvalidOperationException($"Jwt:SigningKey chưa cấu hình hoặc ngắn hơn {JwtOptions.MinSigningKeyBytes} byte (dotnet user-secrets hoặc biến môi trường Jwt__SigningKey).");
        }

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
                    Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = Security.RoomSessionTokenValidator.ValidateAsync
            };
        });

        services.AddAuthorization(options =>
        {
            foreach (var (policy, role) in Policies.RoleOf)
            {
                options.AddPolicy(policy, p => p.RequireRole(role).RequireClaim(AppClaims.Channel, Policies.ChannelOf(role)));
            }

            options.AddPolicy(Policies.AnyChannel, p => p.RequireAssertion(context =>
                Policies.RoleOf.Values.Any(role => context.User.IsInRole(role)
                    && context.User.HasClaim(AppClaims.Channel, Policies.ChannelOf(role)))));

            options.AddPolicy(Policies.ReceptionistOrAdmin, p => p.RequireAssertion(context =>
                (context.User.IsInRole(Roles.Receptionist) || context.User.IsInRole(Roles.Admin))
                && context.User.HasClaim(AppClaims.Channel, Channels.Staff)));
        });

        return services;
    }
}
