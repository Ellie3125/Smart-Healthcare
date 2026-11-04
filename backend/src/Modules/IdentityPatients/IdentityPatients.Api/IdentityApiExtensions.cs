using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPatients.Api;

public static class IdentityApiExtensions
{
    public const string PolicyStaffOnly = "StaffOnly";
    public const string PolicyAdminOnly = "AdminOnly";

    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)];
        var issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? "SmartHealthcare";
        var audience = jwtSection[nameof(JwtOptions.Audience)] ?? "SmartHealthcare";

        // Fallback key nếu dev chưa cấu hình key ngoài biến môi trường (không dùng trên prod)
        var keyBytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(signingKey)
            ? "SmartHealthcareDevSecretKeyMustBeAtLeast32BytesLong!"
            : signingKey);

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
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(15) // ClockSkew nhỏ theo yêu cầu
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyStaffOnly, policy =>
                policy.RequireRole(Roles.Staff));

            options.AddPolicy(PolicyAdminOnly, policy =>
                policy.RequireRole(Roles.Admin));
        });

        return services;
    }
}

