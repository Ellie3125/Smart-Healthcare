namespace IdentityPatients.Contracts;

/// <summary>
/// Tên policy phân quyền. Mỗi policy kiểm cả vai trò và claim <see cref="AppClaims.Channel"/>;
/// Admin chỉ có trong <see cref="AdminOnly"/> (Giải pháp v2.14 "Kênh và vai trò").
/// </summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string ReceptionistOnly = "ReceptionistOnly";
    public const string NurseOnly = "NurseOnly";
    public const string DoctorOnly = "DoctorOnly";
    public const string RoomAccountOnly = "RoomAccountOnly";
    public const string PatientOnly = "PatientOnly";

    /// <summary>Mọi vai trò, nhưng kênh phải khớp loại vai trò (dùng cho /me).</summary>
    public const string AnyChannel = "AnyChannel";

    public static readonly IReadOnlyDictionary<string, string> RoleOf = new Dictionary<string, string>
    {
        [AdminOnly] = Roles.Admin,
        [ReceptionistOnly] = Roles.Receptionist,
        [NurseOnly] = Roles.Nurse,
        [DoctorOnly] = Roles.Doctor,
        [RoomAccountOnly] = Roles.RoomAccount,
        [PatientOnly] = Roles.Patient,
    };

    public static string ChannelOf(string role) => role == Roles.Patient ? Channels.Patient : Channels.Staff;
}
