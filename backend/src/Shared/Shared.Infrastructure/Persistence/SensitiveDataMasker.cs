using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Che CCCD và số điện thoại trong JSON của AuditLog: khóa NationalId, PhoneNumber (không phân biệt hoa thường, mọi cấp)
/// giữ 3 ký tự cuối, còn lại thành '*'; giá trị ≤ 3 ký tự che toàn bộ.
/// </summary>
public static class SensitiveDataMasker
{
    private const int VisibleSuffix = 3;
    private static readonly string[] SensitiveKeys = ["NationalId", "PhoneNumber"];
    private static readonly JsonSerializerOptions Output = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static string? MaskJson(string? json)
    {
        if (json is null)
            return null;
        var root = JsonNode.Parse(json);
        Mask(root);
        return root?.ToJsonString(Output) ?? "null";
    }

    private static void Mask(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    if (value is JsonValue scalar && SensitiveKeys.Contains(name, StringComparer.OrdinalIgnoreCase))
                        obj[name] = MaskValue(scalar.ToString());
                    else
                        Mask(value);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    Mask(item);
                break;
        }
    }

    private static string MaskValue(string value) => value.Length <= VisibleSuffix
        ? new string('*', value.Length)
        : new string('*', value.Length - VisibleSuffix) + value[^VisibleSuffix..];
}
