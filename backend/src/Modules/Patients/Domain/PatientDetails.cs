using System.Text.RegularExpressions;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Domain;

public enum Gender
{
    Male,
    Female,
    Other,
}

/// <summary>Thông tin hành chính đã kiểm tra, dùng khi tạo hoặc sửa hồ sơ.</summary>
public sealed partial record PatientDetails(
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? NationalId,
    string? PhoneNumber,
    string? Address)
{
    public const int FullNameMaxLength = 150;
    public const int AddressMaxLength = 300;
    private static readonly DateOnly EarliestBirthDate = new(1900, 1, 1);

    /// <param name="today">Ngày hiện tại theo giờ Việt Nam, để chặn ngày sinh ở tương lai.</param>
    public static Result<PatientDetails> Create(
        string? fullName,
        DateOnly? dateOfBirth,
        string? gender,
        string? nationalId,
        string? phoneNumber,
        string? address,
        DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();

        var name = CollapseSpaces(fullName);
        if (name.Length is < 2 or > FullNameMaxLength)
            errors["fullName"] = [$"Họ tên cần từ 2 đến {FullNameMaxLength} ký tự."];

        if (dateOfBirth is not { } dob)
            errors["dateOfBirth"] = ["Nhập ngày sinh."];
        else if (dob > today || dob < EarliestBirthDate)
            errors["dateOfBirth"] = ["Ngày sinh không hợp lệ."];

        if (!DomainEnum.TryParse<Gender>(gender, out var parsedGender))
            errors["gender"] = ["Giới tính là Male, Female hoặc Other."];

        var cccd = NullIfBlank(nationalId);
        if (cccd is not null && !NationalIdPattern().IsMatch(cccd))
            errors["nationalId"] = ["CCCD gồm đúng 12 chữ số."];

        var phone = NullIfBlank(phoneNumber) is { } p ? PhoneSeparators().Replace(p, "") : null;
        if (phone is not null && !PhonePattern().IsMatch(phone))
            errors["phoneNumber"] = ["Số điện thoại cần 10 chữ số, bắt đầu bằng 0."];

        var addr = NullIfBlank(address);
        if (addr is { Length: > AddressMaxLength })
            errors["address"] = [$"Địa chỉ tối đa {AddressMaxLength} ký tự."];

        if (errors.Count > 0) return Error.Validation(errors);
        return new PatientDetails(name, dateOfBirth!.Value, parsedGender, cccd, phone, addr);
    }

    private static string CollapseSpaces(string? value) =>
        string.Join(' ', (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex NationalIdPattern();

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex PhonePattern();
}
