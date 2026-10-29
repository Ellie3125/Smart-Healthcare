using System.Text.RegularExpressions;

namespace SmartHealthcare.Identity.Domain;

/// <summary>Chuẩn hóa và kiểm tra tên đăng nhập, email, số điện thoại trước khi lưu hoặc so khớp.</summary>
public static partial class AccountIdentifiers
{
    public const int UserNameMaxLength = 50;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 15;

    public static string NormalizeUserName(string value) => value.Trim();

    public static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch; đổi tiền tố +84 hoặc 84 về 0.</summary>
    public static string NormalizePhone(string value)
    {
        var digits = PhoneSeparators().Replace(value.Trim(), "");
        if (digits.StartsWith("+84", StringComparison.Ordinal)) return "0" + digits[3..];
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11) return "0" + digits[2..];
        return digits;
    }

    public static bool IsValidUserName(string normalized) => UserNamePattern().IsMatch(normalized);

    public static bool IsValidEmail(string normalized) =>
        normalized.Length <= EmailMaxLength && EmailPattern().IsMatch(normalized);

    public static bool IsValidPhone(string normalized) => PhonePattern().IsMatch(normalized);

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,50}$")]
    private static partial Regex UserNamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex PhonePattern();
}
