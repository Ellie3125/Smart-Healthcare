namespace IdentityPatients.Application.Common;

public static class SeedConstants
{
    // Specialties
    public static readonly Guid SpecialtyXQuangId = Guid.Parse("11111111-1111-1111-1111-111111110001");
    public const string SpecialtyXQuangCode = "XQUANG";
    public const string SpecialtyXQuangName = "Chẩn đoán hình ảnh - X-Quang";

    // Doctor Ranks
    public static readonly Guid RankSpecialistIId = Guid.Parse("22222222-2222-2222-2222-222222220001");
    public static readonly Guid RankDoctorId = Guid.Parse("22222222-2222-2222-2222-222222220002");

    // Rooms
    public static readonly Guid RoomXQ01Id = Guid.Parse("33333333-3333-3333-3333-333333330001");
    public const string RoomXQ01Code = "XQ01";
    public const string RoomXQ01Name = "Phòng Chụp X-Quang 01";

    public static readonly Guid RoomXQ02Id = Guid.Parse("33333333-3333-3333-3333-333333330002");
    public const string RoomXQ02Code = "XQ02";
    public const string RoomXQ02Name = "Phòng Chụp X-Quang 02";

    // Staff
    public static readonly Guid StaffReceptionist01Id = Guid.Parse("44444444-4444-4444-4444-444444440001");
    public static readonly Guid StaffNurse01Id = Guid.Parse("44444444-4444-4444-4444-444444440002");
    public static readonly Guid StaffDoctor01Id = Guid.Parse("44444444-4444-4444-4444-444444440003");

    // Doctor
    public static readonly Guid Doctor01Id = Guid.Parse("55555555-5555-5555-5555-555555550001");

    // Room Accounts
    public static readonly Guid RoomAccount01Id = Guid.Parse("66666666-6666-6666-6666-666666660001");
    public static readonly Guid RoomAccount02Id = Guid.Parse("66666666-6666-6666-6666-666666660002");

    // Patient
    public static readonly Guid Patient01Id = Guid.Parse("77777777-7777-7777-7777-777777770001");

    // AppUsers
    public static readonly Guid UserPatient01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
    public const string UserPatient01UserName = "patient01";

    public static readonly Guid UserReceptionist01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
    public const string UserReceptionist01UserName = "letan01";

    public static readonly Guid UserNurse01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003");
    public const string UserNurse01UserName = "dieuduong01";

    public static readonly Guid UserDoctor01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004");
    public const string UserDoctor01UserName = "bacsi01";

    public static readonly Guid UserRoomXQ01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0005");
    public const string UserRoomXQ01UserName = "phong_xq01";

    public static readonly Guid UserRoomXQ02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006");
    public const string UserRoomXQ02UserName = "phong_xq02";

    public static readonly Guid UserAdmin01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007");
    public const string UserAdmin01UserName = "admin01";
}

