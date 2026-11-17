using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPatients.Infrastructure.Security;

internal static class RoomSessionTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null || !principal.IsInRole(Roles.RoomAccount))
        {
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetService<IdentityPatientsDbContext>();
        if (dbContext is null)
        {
            return;
        }

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(sub, out var userId))
        {
            context.Fail("Token không hợp lệ cho tài khoản phòng.");
            return;
        }

        var ct = context.HttpContext.RequestAborted;
        var rawSessionToken = principal.FindFirstValue(ITokenService.RoomSessionClaim);
        if (string.IsNullOrWhiteSpace(rawSessionToken))
        {
            var hasRealRoomAccount = await dbContext.RoomAccounts
                .AsNoTracking()
                .AnyAsync(r => r.AppUserId == userId, ct);

            if (hasRealRoomAccount)
            {
                context.Fail("Phiên thiết bị phòng thiếu thông tin xác thực phiên.");
            }

            return;
        }

        var sessionHash = HashSessionToken(rawSessionToken);
        var isSessionActive = await (
            from session in dbContext.RoomDeviceSessions.AsNoTracking()
            join account in dbContext.RoomAccounts.AsNoTracking() on session.RoomAccountId equals account.Id
            join room in dbContext.Rooms.AsNoTracking() on account.RoomId equals room.Id
            where account.AppUserId == userId
                  && account.IsActive
                  && room.IsActive
                  && session.SessionToken == sessionHash
                  && session.IsActive
            select session.Id).AnyAsync(ct);

        if (!isSessionActive)
        {
            context.Fail("Phiên đăng nhập thiết bị phòng đã bị đóng hoặc đăng xuất.");
        }
    }

    public static string HashSessionToken(string rawSessionToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSessionToken))).ToLowerInvariant();
}
