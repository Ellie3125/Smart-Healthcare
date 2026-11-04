namespace IdentityPatients.Contracts.Constants;

public static class Roles
{
    public const string Patient = "Patient";
    public const string Receptionist = "Receptionist";
    public const string Nurse = "Nurse";
    public const string Doctor = "Doctor";
    public const string RoomAccount = "RoomAccount";
    public const string Admin = "Admin";

    public static readonly string[] Staff = [Receptionist, Nurse, Doctor, RoomAccount, Admin];
}

