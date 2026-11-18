using System.Threading.RateLimiting;
using IdentityPatients.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Api;

public static class DependencyInjection
{
    public const string Tag = "IdentityPatients";
    public const string PatientRegisterRateLimitPolicy = "patient-register";

    public static IMvcBuilder AddIdentityPatientsApi(this IMvcBuilder builder) =>
        builder.AddApplicationPart(typeof(AuthController).Assembly);

    public static IServiceCollection AddIdentityPatientsApi(this IServiceCollection services) => services;

    /// <summary>Cấu hình rate limit fixed-window cho endpoint đăng ký tài khoản bệnh nhân.</summary>
    public static IServiceCollection AddPatientRegisterRateLimiter(
        this IServiceCollection services,
        int permitLimit = 10,
        TimeSpan? window = null)
    {
        var resolvedWindow = window ?? TimeSpan.FromMinutes(1);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                var problemDetails = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>()
                    .CreateProblemDetails(
                        context.HttpContext,
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Quá nhiều yêu cầu",
                        detail: "Bạn đã gửi quá nhiều yêu cầu đăng ký trong thời gian ngắn. Vui lòng thử lại sau ít phút.");
                problemDetails.Extensions["code"] = "TooManyRequests";
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, token);
            };
            options.AddPolicy(PatientRegisterRateLimitPolicy, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ip,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = resolvedWindow,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    public static RouteGroupBuilder MapIdentityPatientsEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/identity-patients").WithTags(Tag);
}
