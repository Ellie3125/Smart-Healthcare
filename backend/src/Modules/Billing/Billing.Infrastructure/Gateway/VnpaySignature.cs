using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Billing.Infrastructure.Gateway;

/// <summary>
/// Chữ ký VNPAY 2.1.0: HMAC-SHA512 trên chuỗi key=value sắp theo tên tham số, đã URL-encode (khoảng trắng thành dấu +).
/// Chỉ tính các tham số vnp_ có giá trị, trừ chính vnp_SecureHash và vnp_SecureHashType.
/// </summary>
internal static class VnpaySignature
{
    public const string HashParameter = "vnp_SecureHash";
    private const string HashTypeParameter = "vnp_SecureHashType";

    public static string Canonical(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join('&', parameters
            .Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal) && p.Key is not (HashParameter or HashTypeParameter)
                && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => string.Create(CultureInfo.InvariantCulture, $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}")));

    public static string Sign(string secret, string canonical) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));

    public static bool Verify(string secret, IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(HashParameter, out var received) || received.Length != HMACSHA512.HashSizeInBytes * 2)
            return false;

        var expected = Sign(secret, Canonical(parameters));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received.ToLowerInvariant()));
    }
}
