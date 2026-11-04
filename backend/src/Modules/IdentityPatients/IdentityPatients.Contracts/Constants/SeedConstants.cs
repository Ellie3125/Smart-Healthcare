namespace IdentityPatients.Contracts.Constants;

public static class SeedConstants
{
    // Chuyên khoa (mã cố định để chatbot và Scheduling tham chiếu)
    public static readonly Guid NoiTongQuatSpecialtyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const string NoiTongQuatCode = "NOI_TONG_QUAT";
    public const string NoiTongQuatName = "Nội tổng quát";

    // Cấp bậc bác sĩ (2 cấp bậc)
    public static readonly Guid RankBacSiChuyenKhoaId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    public const string RankBacSiChuyenKhoaCode = "CK1";
    public const string RankBacSiChuyenKhoaName = "Bác sĩ Chuyên khoa I";

    public static readonly Guid RankTienSiBacSiId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string RankTienSiBacSiCode = "TS_BS";
    public const string RankTienSiBacSiName = "Tiến sĩ - Bác sĩ";

    // Phòng khám và phòng CLS (2 phòng X-quang: XQ01, XQ02, 1 phòng khám)
    public static readonly Guid RoomPhongKham01Id = Guid.Parse("33333333-3333-3333-3333-333333333301");
    public const string RoomPhongKham01Code = "PK01";
    public const string RoomPhongKham01Name = "Phòng Khám Nội 01";

    public static readonly Guid RoomXQ01Id = Guid.Parse("33333333-3333-3333-3333-333333333311");
    public const string RoomXQ01Code = "XQ01";
    public const string RoomXQ01Name = "Phòng Chụp X-Quang 01";

    public static readonly Guid RoomXQ02Id = Guid.Parse("33333333-3333-3333-3333-333333333312");
    public const string RoomXQ02Code = "XQ02";
    public const string RoomXQ02Name = "Phòng Chụp X-Quang 02";

    // Bác sĩ
    public static readonly Guid Doctor01Id = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public const string Doctor01FullName = "BS. Nguyễn Văn An";

    // Tài khoản người dùng (AppUser)
    public static readonly Guid UserPatient01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
    public static readonly Guid UserLetan01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
    public static readonly Guid UserDieuduong01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003");
    public static readonly Guid UserBacsi01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004");
    public static readonly Guid UserPhongXQ01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0005");
    public static readonly Guid UserPhongXQ02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006");
    public static readonly Guid UserAdmin01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007");

    // Hồ sơ bệnh nhân mẫu
    public static readonly Guid Patient01Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001");
}

