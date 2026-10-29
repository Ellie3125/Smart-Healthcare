using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Tài khoản, nhân sự, bác sĩ, phòng và phiên thiết bị phòng (sơ đồ 1).</summary>
internal static class IdentityAndStaffTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("AppUser", t =>
        {
            t.Id();
            t.Req<string>("UserName", "nvarchar(50)");
            t.Req<string>("Email", "nvarchar(256)");
            t.Req<string>("PhoneNumber", "varchar(15)");
            t.Req<string>("PasswordHash", "nvarchar(500)");
            t.Flag("EmailConfirmed", false);
            t.Flag("PhoneNumberConfirmed", false);
            t.Flag("IsActive", true);
            t.UtcNow("CreatedAtUtc");
            t.Unique("UserName");
            t.Unique("Email");
            t.Unique("PhoneNumber");
        });

        model.Table("Room", t =>
        {
            t.Id();
            t.Req<string>("RoomNumber", "varchar(20)");
            t.Req<string>("RoomName", "nvarchar(150)");
            t.Enum("RoomType", 20, "Clinical", "Laboratory", "XRay", "Ultrasound", "Endoscopy");
            t.Opt<string>("DepartmentName", "nvarchar(150)");
            t.Flag("IsActive", true);
            t.Unique("RoomNumber");
        });

        model.Table("Specialty", t =>
        {
            t.Id();
            t.Req<string>("SpecialtyCode", "varchar(30)");
            t.Req<string>("SpecialtyName", "nvarchar(150)");
            t.Opt<string>("Description", "nvarchar(1000)");
            t.Flag("IsActive", true);
            t.Unique("SpecialtyCode");
        });

        model.Table("DoctorRank", t =>
        {
            t.Id();
            t.Req<string>("RankCode", "varchar(30)"); // SpecialistI, SpecialistII, Master, Doctor, AssociateProf, Professor
            t.Req<string>("RankName", "nvarchar(100)");
            t.Req<int>("DisplayOrder", "int");
            t.Flag("IsActive", true);
            t.Unique("RankCode");
        });

        model.Table("Staff", t =>
        {
            t.Id();
            t.Ref("AppUserId", "AppUser");
            t.Req<string>("StaffCode", "varchar(20)");
            t.Req<string>("FullName", "nvarchar(150)");
            t.Enum("StaffType", 20, "Doctor", "Nurse", "Receptionist", "Technician");
            t.Flag("IsActive", true);
            t.UtcNow("CreatedAtUtc");
            t.Unique("AppUserId");
            t.Unique("StaffCode");
        });

        model.Table("RoomAccount", t =>
        {
            t.Id();
            t.Ref("AppUserId", "AppUser");
            t.Ref("RoomId", "Room");
            t.Req<string>("AccountCode", "varchar(30)");
            t.Flag("IsActive", true);
            t.UtcNow("CreatedAtUtc");
            t.Unique("AppUserId");
            t.Unique("RoomId");
            t.Unique("AccountCode");
        });

        model.Table("RoomDeviceSession", t =>
        {
            t.Id();
            t.Ref("RoomAccountId", "RoomAccount");
            t.Req<string>("DeviceId", "nvarchar(100)");
            t.Opt<string>("DeviceIpAddress", "varchar(45)");
            t.Req<string>("SessionToken", "varchar(200)");
            t.Utc("LoginAtUtc");
            t.OptUtc("LogoutAtUtc");
            t.Flag("IsActive", true);
            t.Unique("SessionToken");
        });

        model.Table("Doctor", t =>
        {
            t.Id();
            t.Ref("StaffId", "Staff");
            t.Ref("SpecialtyId", "Specialty");
            t.Ref("DoctorRankId", "DoctorRank");
            t.Opt<string>("Biography", "nvarchar(max)");
            t.Flag("IsActive", true);
            t.Unique("StaffId");
        });

        model.Table("StaffShiftAssignment", t =>
        {
            t.Id();
            t.Ref("StaffId", "Staff");
            t.Ref("RoomId", "Room");
            t.Req<DateOnly>("ShiftDate", "date");
            t.Enum("ShiftName", 10, "Morning", "Afternoon", "FullDay");
            t.Utc("StartsAtUtc");
            t.Utc("EndsAtUtc");
            t.Flag("IsShiftLeader", false);
            t.Check("CK_StaffShiftAssignment_Range", "EndsAtUtc > StartsAtUtc");
        });
    }
}
