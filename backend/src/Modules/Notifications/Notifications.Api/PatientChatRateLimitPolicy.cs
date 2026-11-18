using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Notifications.Api;

/// <summary>10 lượt chat/phút mỗi tài khoản (contract chatbot §3.1). OnRejected riêng: 429 + code, không dùng thông điệp của policy khác.</summary>
internal sealed class PatientChatRateLimitPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
        await response.WriteAsJsonAsync(new { title = "Quá nhiều tin nhắn, vui lòng thử lại sau.", status = 429, code = "TooManyRequests" },
            ct);
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext) => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.User.FindFirstValue("sub") ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
