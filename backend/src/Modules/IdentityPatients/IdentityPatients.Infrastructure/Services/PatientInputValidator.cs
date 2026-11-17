using System.Text.RegularExpressions;
using IdentityPatients.Domain.Enums;

namespace IdentityPatients.Infrastructure.Services;

internal sealed record ValidatedPatientFields(
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? NationalId,
    string PhoneNumber,
    string Address);

internal static partial class PatientInputValidator
{
    public const int MaxFullNameLength = 200;
    public const int MaxAddressLength = 200;
    public const int MaxPhoneLength = 32;
    public const int MaxAgeYears = 120;
    public const string DuplicateNationalIdMessage = "Số CCCD đã được đăng ký, vui lòng liên hệ quầy tiếp tân";

    [GeneratedRegex(@"^(0(3|5|7|8|9)\d{8}|02\d{8,9}|\+84((3|5|7|8|9)\d{8}|2\d{8,9}))$", RegexOptions.CultureInvariant)]
    private static partial Regex VietnamPhoneRegex();

    public static (string? Normalized, (string Title, string Detail, string Code)? Error) ValidateVietnamesePhone(
        string? rawPhone,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return required
                ? (null, ("Dữ liệu không hợp lệ", "Số điện thoại không được để trống.", "InvalidPhoneNumber"))
                : (null, null);
        }

        var trimmed = rawPhone.Trim();
        if (trimmed.Length > MaxPhoneLength)
        {
            return (null, ("Dữ liệu không hợp lệ", $"Số điện thoại không được vượt quá {MaxPhoneLength} ký tự.", "InvalidPhoneNumberLength"));
        }

        var compact = trimmed.Replace(" ", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty);
        if (!VietnamPhoneRegex().IsMatch(compact))
        {
            return (null, ("Dữ liệu không hợp lệ", "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 09xxxxxxxx, 028xxxxxxxx hoặc +849xxxxxxxx).", "InvalidPhoneNumberFormat"));
        }

        return (trimmed, null);
    }

    public static (ValidatedPatientFields? Fields, (string Title, string Detail, string Code)? Error) ValidatePatientFields(
        string? fullName,
        DateOnly dateOfBirth,
        string? genderText,
        string? nationalId,
        string? phoneNumber,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (null, ("Dữ liệu không hợp lệ", "Họ và tên bệnh nhân không được để trống.", "InvalidFullName"));
        }

        var normalizedFullName = fullName.Trim();
        if (normalizedFullName.Length > MaxFullNameLength)
        {
            return (null, ("Dữ liệu không hợp lệ", $"Họ và tên bệnh nhân không được vượt quá {MaxFullNameLength} ký tự.", "InvalidFullNameLength"));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dateOfBirth > today || dateOfBirth < today.AddYears(-MaxAgeYears))
        {
            return (null, ("Dữ liệu không hợp lệ", $"Ngày sinh không hợp lệ (không được ở tương lai và không quá {MaxAgeYears} tuổi).", "InvalidDateOfBirth"));
        }

        if (string.IsNullOrWhiteSpace(genderText)
            || !Enum.TryParse<Gender>(genderText.Trim(), ignoreCase: true, out var gender)
            || !Enum.IsDefined(gender))
        {
            return (null, ("Dữ liệu không hợp lệ", "Giới tính không hợp lệ (Male, Female, Other).", "InvalidGender"));
        }

        var normalizedNationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        if (normalizedNationalId is not null
            && (normalizedNationalId.Length != 12 || !normalizedNationalId.All(char.IsAsciiDigit)))
        {
            return (null, ("Dữ liệu không hợp lệ", "Số CCCD phải gồm đúng 12 chữ số.", "InvalidNationalId"));
        }

        var (normalizedPhone, phoneError) = ValidateVietnamesePhone(phoneNumber, required: true);
        if (phoneError is not null)
        {
            return (null, phoneError);
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return (null, ("Dữ liệu không hợp lệ", "Địa chỉ không được để trống.", "InvalidAddress"));
        }

        var normalizedAddress = address.Trim();
        if (normalizedAddress.Length > MaxAddressLength)
        {
            return (null, ("Dữ liệu không hợp lệ", $"Địa chỉ không được vượt quá {MaxAddressLength} ký tự.", "InvalidAddressLength"));
        }

        return (new ValidatedPatientFields(
            normalizedFullName,
            dateOfBirth,
            gender,
            normalizedNationalId,
            normalizedPhone!,
            normalizedAddress), null);
    }

    public static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
        {
            return null;
        }

        var trimmed = nationalId.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}
