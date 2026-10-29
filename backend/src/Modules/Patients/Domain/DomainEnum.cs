namespace SmartHealthcare.Patients.Domain;

public static class DomainEnum
{
    /// <summary>Chỉ nhận đúng tên giá trị (không phân biệt hoa thường); từ chối chuỗi số như "1" mà Enum.TryParse vẫn chấp nhận.</summary>
    public static bool TryParse<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var match = Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match is not null && Enum.TryParse(match, out result);
    }
}
