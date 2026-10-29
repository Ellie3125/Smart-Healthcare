using System.Security.Claims;

namespace SmartHealthcare.Identity.Contracts;

/// <summary>Claim trong access token do module Identity phát hành. Module khác đọc người gọi qua đây, không đọc bảng tài khoản.</summary>
public static class AuthenticatedUser
{
    public const string UserIdClaim = "sub";
    public const string RoleClaim = "role";

    /// <summary>Policy cho chức năng tự phục vụ của bệnh nhân/người đại diện (kênh public).</summary>
    public const string PatientAccountPolicy = "PatientAccount";

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst(UserIdClaim)?.Value, out userId);
}

public static class AccountRoles
{
    public const string Patient = "Patient";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string Receptionist = "Receptionist";
    public const string Technician = "Technician";
}
