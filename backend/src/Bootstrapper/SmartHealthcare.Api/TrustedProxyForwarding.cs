using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SmartHealthcare.Api;

/// <summary>
/// Staging/Prod chạy sau Cloudflare Tunnel và Nginx (Giải pháp kỹ thuật §8.6): không xử lý thì RemoteIpAddress là IP của Nginx,
/// nên vnp_IpAddr gửi VNPAY, IP trong AuditLog và rate limit đăng ký theo IP đều sai. Nginx ghi IP người dùng (Cf-Connecting-Ip)
/// vào X-Forwarded-For; Api chỉ tin header này khi request đến từ mạng proxy khai trong <c>ForwardedHeaders:KnownNetworks</c>
/// (CIDR, ví dụ mạng Docker của Nginx). Không khai thì tắt hẳn: máy dev không có proxy, header do client tự gửi bị bỏ qua.
/// </summary>
internal static class TrustedProxyForwarding
{
    private const string NetworksKey = "ForwardedHeaders:KnownNetworks";

    public static IServiceCollection AddTrustedProxyForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        var networks = Networks(configuration);
        if (networks.Count == 0)
            return services;

        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Nginx là proxy duy nhất ghi header; chỉ lấy một bước gần nhất.
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in networks)
                options.KnownIPNetworks.Add(network);
        });
    }

    public static IApplicationBuilder UseTrustedProxyForwarding(this WebApplication app) =>
        Networks(app.Configuration).Count == 0 ? app : app.UseForwardedHeaders();

    private static List<System.Net.IPNetwork> Networks(IConfiguration configuration) =>
        (configuration.GetSection(NetworksKey).Get<string[]>() ?? [])
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => System.Net.IPNetwork.TryParse(n.Trim(), out var network)
            ? network
            : throw new InvalidOperationException($"{NetworksKey} có giá trị không phải CIDR: '{n}'."))
        .ToList();
}
