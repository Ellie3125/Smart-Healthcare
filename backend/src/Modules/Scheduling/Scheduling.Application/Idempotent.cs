using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scheduling.Application;

/// <summary>Quy ước idempotency của lệnh Scheduling (Giải pháp v2.14 §4): phạm vi theo AppUser, giữ bản ghi 24 giờ.</summary>
internal static class Idempotent
{
    public const int MaxKeyLength = 64;
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public static string Scope(Guid appUserId) => $"AppUser:{appUserId:D}";

    public static bool IsValidKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxKeyLength;

    public static string Hash(params object[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts))));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    public static T Deserialize<T>(string? json) =>
        JsonSerializer.Deserialize<T>(json ?? throw new InvalidOperationException("Bản ghi idempotency đã hoàn tất nhưng không có body."))!;
}
