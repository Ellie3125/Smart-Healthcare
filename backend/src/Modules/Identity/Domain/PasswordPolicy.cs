namespace SmartHealthcare.Identity.Domain;

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    /// <summary>Trả về lý do không đạt, hoặc null nếu mật khẩu hợp lệ.</summary>
    public static string? Check(string password)
    {
        if (password.Length < MinLength) return $"Mật khẩu cần ít nhất {MinLength} ký tự.";
        if (password.Length > MaxLength) return $"Mật khẩu tối đa {MaxLength} ký tự.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Mật khẩu cần có cả chữ và số.";
        return null;
    }
}
