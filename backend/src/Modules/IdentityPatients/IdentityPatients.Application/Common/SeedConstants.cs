namespace IdentityPatients.Application.Common;

public static class SeedConstants
{
    // Specialties
    public static readonly Guid SpecialtyXQuangId = Guid.Parse("11111111-1111-1111-1111-111111110001");
    public const string SpecialtyXQuangCode = "XQUANG";
    public const string SpecialtyXQuangName = "Chẩn đoán hình ảnh - X-Quang";

    public static readonly Guid SpecialtyTieuHoaId = Guid.Parse("11111111-1111-1111-1111-111111110002");
    public const string SpecialtyTieuHoaCode = "TIEU_HOA";
    public const string SpecialtyTieuHoaName = "Khoa Tiêu hóa";

    public static readonly Guid SpecialtyTaiMuiHongId = Guid.Parse("11111111-1111-1111-1111-111111110003");
    public const string SpecialtyTaiMuiHongCode = "TAI_MUI_HONG";
    public const string SpecialtyTaiMuiHongName = "Khoa Tai Mũi Họng";

    public static readonly Guid SpecialtyDaLieuId = Guid.Parse("11111111-1111-1111-1111-111111110004");
    public const string SpecialtyDaLieuCode = "DA_LIEU";
    public const string SpecialtyDaLieuName = "Khoa Da liễu";

    public static readonly Guid SpecialtyXetNghiemId = Guid.Parse("11111111-1111-1111-1111-111111110005");
    public const string SpecialtyXetNghiemCode = "XET_NGHIEM";
    public const string SpecialtyXetNghiemName = "Khoa Xét nghiệm y học";

    public static readonly Guid SpecialtyNoiSoiId = Guid.Parse("11111111-1111-1111-1111-111111110006");
    public const string SpecialtyNoiSoiCode = "NOI_SOI";
    public const string SpecialtyNoiSoiName = "Khoa Nội soi";

    public static readonly Guid SpecialtySieuAmId = Guid.Parse("11111111-1111-1111-1111-111111110007");
    public const string SpecialtySieuAmCode = "SIEU_AM";
    public const string SpecialtySieuAmName = "Khoa Siêu âm";

    public static readonly Guid SpecialtyNoiTongQuatId = Guid.Parse("11111111-1111-1111-1111-111111110008");
    public const string SpecialtyNoiTongQuatCode = "NOI_TONG_QUAT";
    public const string SpecialtyNoiTongQuatName = "Khoa Nội tổng quát";

    public static readonly Guid SpecialtyTimMachId = Guid.Parse("11111111-1111-1111-1111-111111110009");
    public const string SpecialtyTimMachCode = "TIM_MACH";
    public const string SpecialtyTimMachName = "Khoa Tim mạch";

    public static readonly Guid SpecialtyHoHapId = Guid.Parse("11111111-1111-1111-1111-111111110010");
    public const string SpecialtyHoHapCode = "HO_HAP";
    public const string SpecialtyHoHapName = "Khoa Hô hấp";

    public static readonly Guid SpecialtyNhiId = Guid.Parse("11111111-1111-1111-1111-111111110011");
    public const string SpecialtyNhiCode = "NHI";
    public const string SpecialtyNhiName = "Khoa Nhi";

    // Doctor Ranks
    public static readonly Guid RankSpecialistIId = Guid.Parse("22222222-2222-2222-2222-222222220001");
    public static readonly Guid RankSpecialistIIId = Guid.Parse("22222222-2222-2222-2222-222222220002");
    public static readonly Guid RankMasterId = Guid.Parse("22222222-2222-2222-2222-222222220003");
    public static readonly Guid RankDoctorId = Guid.Parse("22222222-2222-2222-2222-222222220004");
    public static readonly Guid RankAssociateProfId = Guid.Parse("22222222-2222-2222-2222-222222220005");
    public static readonly Guid RankProfessorId = Guid.Parse("22222222-2222-2222-2222-222222220006");

    // X-Ray Rooms
    public static readonly Guid RoomXQ01Id = Guid.Parse("33333333-3333-3333-3333-333333330001");
    public const string RoomXQ01Code = "XQ01";
    public const string RoomXQ01Name = "Phòng Chụp X-Quang 01";

    public static readonly Guid RoomXQ02Id = Guid.Parse("33333333-3333-3333-3333-333333330002");
    public const string RoomXQ02Code = "XQ02";
    public const string RoomXQ02Name = "Phòng Chụp X-Quang 02";

    // CLS Rooms
    public static readonly Guid RoomXN01Id = Guid.Parse("33333333-3333-3333-3333-333333330003");
    public const string RoomXN01Code = "XN01";
    public const string RoomXN01Name = "Phòng Xét nghiệm Y học 01";

    public static readonly Guid RoomSA01Id = Guid.Parse("33333333-3333-3333-3333-333333330004");
    public const string RoomSA01Code = "SA01";
    public const string RoomSA01Name = "Phòng Siêu âm Tổng quát 01";

    public static readonly Guid RoomSA02Id = Guid.Parse("33333333-3333-3333-3333-333333330005");
    public const string RoomSA02Code = "SA02";
    public const string RoomSA02Name = "Phòng Siêu âm Tim mạch 02";

    public static readonly Guid RoomNS01Id = Guid.Parse("33333333-3333-3333-3333-333333330006");
    public const string RoomNS01Code = "NS01";
    public const string RoomNS01Name = "Phòng Nội soi Tiêu hóa 01";

    // 10 Clinical Rooms
    public static readonly Guid RoomPK01Id = Guid.Parse("33333333-3333-3333-3333-333333330011");
    public static readonly Guid RoomPK02Id = Guid.Parse("33333333-3333-3333-3333-333333330012");
    public static readonly Guid RoomPK03Id = Guid.Parse("33333333-3333-3333-3333-333333330013");
    public static readonly Guid RoomPK04Id = Guid.Parse("33333333-3333-3333-3333-333333330014");
    public static readonly Guid RoomPK05Id = Guid.Parse("33333333-3333-3333-3333-333333330015");
    public static readonly Guid RoomPK06Id = Guid.Parse("33333333-3333-3333-3333-333333330016");
    public static readonly Guid RoomPK07Id = Guid.Parse("33333333-3333-3333-3333-333333330017");
    public static readonly Guid RoomPK08Id = Guid.Parse("33333333-3333-3333-3333-333333330018");
    public static readonly Guid RoomPK09Id = Guid.Parse("33333333-3333-3333-3333-333333330019");
    public static readonly Guid RoomPK10Id = Guid.Parse("33333333-3333-3333-3333-333333330020");

    // Staff
    public static readonly Guid StaffReceptionist01Id = Guid.Parse("44444444-4444-4444-4444-444444440001");
    public static readonly Guid StaffNurse01Id = Guid.Parse("44444444-4444-4444-4444-444444440002");
    public static readonly Guid StaffAdmin01Id = Guid.Parse("44444444-4444-4444-4444-444444440020");
    public static readonly Guid StaffDoctor01Id = Guid.Parse("44444444-4444-4444-4444-444444440003");
    public static readonly Guid StaffDoctor02Id = Guid.Parse("44444444-4444-4444-4444-444444440004");
    public static readonly Guid StaffDoctor03Id = Guid.Parse("44444444-4444-4444-4444-444444440005");
    public static readonly Guid StaffDoctor04Id = Guid.Parse("44444444-4444-4444-4444-444444440006");
    public static readonly Guid StaffDoctor05Id = Guid.Parse("44444444-4444-4444-4444-444444440007");
    public static readonly Guid StaffDoctor06Id = Guid.Parse("44444444-4444-4444-4444-444444440008");
    public static readonly Guid StaffDoctor07Id = Guid.Parse("44444444-4444-4444-4444-444444440021");
    public static readonly Guid StaffDoctor08Id = Guid.Parse("44444444-4444-4444-4444-444444440022");
    public static readonly Guid StaffDoctor09Id = Guid.Parse("44444444-4444-4444-4444-444444440023");
    public static readonly Guid StaffDoctor10Id = Guid.Parse("44444444-4444-4444-4444-444444440024");

    // Doctors
    public static readonly Guid Doctor01Id = Guid.Parse("55555555-5555-5555-5555-555555550001");
    public static readonly Guid Doctor02Id = Guid.Parse("55555555-5555-5555-5555-555555550002");
    public static readonly Guid Doctor03Id = Guid.Parse("55555555-5555-5555-5555-555555550003");
    public static readonly Guid Doctor04Id = Guid.Parse("55555555-5555-5555-5555-555555550004");
    public static readonly Guid Doctor05Id = Guid.Parse("55555555-5555-5555-5555-555555550005");
    public static readonly Guid Doctor06Id = Guid.Parse("55555555-5555-5555-5555-555555550006");
    public static readonly Guid Doctor07Id = Guid.Parse("55555555-5555-5555-5555-555555550007");
    public static readonly Guid Doctor08Id = Guid.Parse("55555555-5555-5555-5555-555555550008");
    public static readonly Guid Doctor09Id = Guid.Parse("55555555-5555-5555-5555-555555550009");
    public static readonly Guid Doctor10Id = Guid.Parse("55555555-5555-5555-5555-555555550010");

    // Room Accounts
    public static readonly Guid RoomAccount01Id = Guid.Parse("66666666-6666-6666-6666-666666660001");
    public static readonly Guid RoomAccount02Id = Guid.Parse("66666666-6666-6666-6666-666666660002");
    public static readonly Guid RoomAccountXN01Id = Guid.Parse("66666666-6666-6666-6666-666666660003");
    public static readonly Guid RoomAccountSA01Id = Guid.Parse("66666666-6666-6666-6666-666666660004");
    public static readonly Guid RoomAccountSA02Id = Guid.Parse("66666666-6666-6666-6666-666666660005");
    public static readonly Guid RoomAccountNS01Id = Guid.Parse("66666666-6666-6666-6666-666666660006");

    // Patients
    public static readonly Guid Patient01Id = Guid.Parse("77777777-7777-7777-7777-777777770001");
    public static readonly Guid Patient02Id = Guid.Parse("77777777-7777-7777-7777-777777770002");
    public static readonly Guid Patient03Id = Guid.Parse("77777777-7777-7777-7777-777777770003");
    public static readonly Guid Patient04Id = Guid.Parse("77777777-7777-7777-7777-777777770004");
    public static readonly Guid Patient05Id = Guid.Parse("77777777-7777-7777-7777-777777770005");
    public static readonly Guid Patient06Id = Guid.Parse("77777777-7777-7777-7777-777777770006");

    // AppUsers
    public static readonly Guid UserPatient01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
    public const string UserPatient01UserName = "patient01";

    public static readonly Guid UserPatient02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0010");
    public const string UserPatient02UserName = "patient02";

    public static readonly Guid UserPatient03Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0030");
    public const string UserPatient03UserName = "patient03";

    public static readonly Guid UserReceptionist01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
    public const string UserReceptionist01UserName = "letan01";

    public static readonly Guid UserNurse01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003");
    public const string UserNurse01UserName = "dieuduong01";

    public static readonly Guid UserDoctor01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004");
    public const string UserDoctor01UserName = "bacsi01";

    public static readonly Guid UserDoctor02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0011");
    public const string UserDoctor02UserName = "bacsi02";

    public static readonly Guid UserDoctor03Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0012");
    public const string UserDoctor03UserName = "bacsi03";

    public static readonly Guid UserDoctor04Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0013");
    public const string UserDoctor04UserName = "bacsi04";

    public static readonly Guid UserDoctor05Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0014");
    public const string UserDoctor05UserName = "bacsi05";

    public static readonly Guid UserDoctor06Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0015");
    public const string UserDoctor06UserName = "bacsi06";

    public static readonly Guid UserDoctor07Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0031");
    public const string UserDoctor07UserName = "bacsi07";

    public static readonly Guid UserDoctor08Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0032");
    public const string UserDoctor08UserName = "bacsi08";

    public static readonly Guid UserDoctor09Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0033");
    public const string UserDoctor09UserName = "bacsi09";

    public static readonly Guid UserDoctor10Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0034");
    public const string UserDoctor10UserName = "bacsi10";

    public static readonly Guid UserRoomXQ01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0005");
    public const string UserRoomXQ01UserName = "phong_xq01";

    public static readonly Guid UserRoomXQ02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006");
    public const string UserRoomXQ02UserName = "phong_xq02";

    public static readonly Guid UserRoomXN01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007");
    public const string UserRoomXN01UserName = "phong_xn01";

    public static readonly Guid UserRoomSA01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0008");
    public const string UserRoomSA01UserName = "phong_sa01";

    public static readonly Guid UserRoomSA02Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0009");
    public const string UserRoomSA02UserName = "phong_sa02";

    public static readonly Guid UserRoomNS01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0016");
    public const string UserRoomNS01UserName = "phong_ns01";

    public static readonly Guid UserAdmin01Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0020");
    public const string UserAdmin01UserName = "admin01";
}
