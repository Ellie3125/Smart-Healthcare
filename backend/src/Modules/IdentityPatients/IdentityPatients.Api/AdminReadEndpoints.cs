using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Application.Administration;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IdentityPatients.Api;

internal static class AdminReadEndpoints
{
    public static void MapAdminReads(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/admin").WithTags(DependencyInjection.Tag).RequireAuthorization(Policies.AdminOnly);
        group.MapGet("/audit-logs", AuditAsync).WithName("Identity.Admin.AuditLogs");
        group.MapGet("/permissions-matrix", MatrixAsync).WithName("Identity.Admin.PermissionsMatrix");
    }

    private static async Task<IResult> AuditAsync(HttpRequest request, IAdminAuditLogReader reader, CancellationToken ct)
    {
        var query = request.Query; var page = 1;
        if (query["page"].Count > 1 || query["keyword"].Count > 1
            || (query.ContainsKey("page") && !int.TryParse(query["page"], NumberStyles.None, CultureInfo.InvariantCulture, out page))
            || page is < 1 or > 1000 || query["keyword"].ToString().Trim().Length > 100)
            return Results.Problem(statusCode: 400, title: "Tham số tra cứu không hợp lệ.", extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await reader.ReadAsync(query["keyword"].ToString(), page, ct));
    }

    private sealed record PermissionRow(string Role, IReadOnlyList<string> Capabilities, string RestrictedNote);
    private static async Task<IResult> MatrixAsync(HttpResponse response, EndpointDataSource source,
        IAuthorizationPolicyProvider policies, IAuthorizationService authorization)
    {
        var endpoints = new List<(RouteEndpoint Endpoint, AuthorizationPolicy Policy)>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) continue;
            var data = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var attached = endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>();
            if (data.Count == 0 && attached.Count == 0) continue;
            var policy = await AuthorizationPolicy.CombineAsync(policies, data, attached);
            if (policy is not null) endpoints.Add((endpoint, policy));
        }
        var result = new List<PermissionRow>();
        foreach (var role in Policies.RoleOf.Values.Distinct().Order(StringComparer.Ordinal))
        {
            var channel = Policies.ChannelOf(role);
            var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, role), new(AppClaims.Channel, channel)], "matrix"));
            var capabilities = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (endpoint, policy) in endpoints)
                if ((await authorization.AuthorizeAsync(user, null, policy)).Succeeded)
                    foreach (var method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                        capabilities.Add(method + " /" + endpoint.RoutePattern.RawText?.TrimStart('/'));
            result.Add(new(role, capabilities.ToArray(), $"Kênh {channel}; chỉ phản ánh policy khai báo trên route. Quyền hồ sơ, phòng, ca và điều kiện nghiệp vụ vẫn được kiểm tra trong handler."));
        }
        response.Headers.CacheControl = "no-store";
        return Results.Ok(result);
    }
}
