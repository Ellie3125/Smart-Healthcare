using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityPatients.Infrastructure.Seed;

public sealed class IdentityPatientsSeedService(
    IdentityPatientsDbContext dbContext,
    IConfiguration configuration,
    ILogger<IdentityPatientsSeedService> logger)
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<IdentityPatientsSeedService> _logger = logger;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var defaultPassword = _configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(defaultPassword))
        {
            throw new InvalidOperationException("Cấu hình Seed:DefaultPassword chưa được đặt trong User Secrets hoặc biến môi trường.");
        }

        _logger.LogInformation("Bắt đầu thực hiện Seed dữ liệu cho module IdentityPatients. Lưu ý: Đây là dữ liệu giả định demo, không phải số liệu bệnh viện thật. ServiceStation không thuộc module này.");

        // 1. Vai trò lưu ở AppUser.Role (không có bảng vai trò, ERD v2.4).

        // 2. Chuyên khoa (Specialties)
        var specialtiesToSeed = new (Guid Id, string Code, string Name, string Desc)[]
        {
            (SeedConstants.SpecialtyXQuangId, SeedConstants.SpecialtyXQuangCode, SeedConstants.SpecialtyXQuangName, "Chẩn đoán hình ảnh kỹ thuật số, X-Quang can thiệp và kiểm tra định kỳ."),
            (SeedConstants.SpecialtyTieuHoaId, SeedConstants.SpecialtyTieuHoaCode, SeedConstants.SpecialtyTieuHoaName, "Khám và điều trị chuyên sâu bệnh lý đường tiêu hóa, gan mật tụy."),
            (SeedConstants.SpecialtyTaiMuiHongId, SeedConstants.SpecialtyTaiMuiHongCode, SeedConstants.SpecialtyTaiMuiHongName, "Khám và phẫu thuật nội soi tai mũi họng người lớn và trẻ em."),
            (SeedConstants.SpecialtyDaLieuId, SeedConstants.SpecialtyDaLieuCode, SeedConstants.SpecialtyDaLieuName, "Chẩn đoán, điều trị bệnh lý da liễu và thẩm mỹ y khoa."),
            (SeedConstants.SpecialtyXetNghiemId, SeedConstants.SpecialtyXetNghiemCode, SeedConstants.SpecialtyXetNghiemName, "Xét nghiệm huyết học, sinh hóa, miễn dịch và vi sinh lâm sàng."),
            (SeedConstants.SpecialtyNoiSoiId, SeedConstants.SpecialtyNoiSoiCode, SeedConstants.SpecialtyNoiSoiName, "Nội soi dạ dày, đại tràng không đau và can thiệp thủ thuật."),
            (SeedConstants.SpecialtySieuAmId, SeedConstants.SpecialtySieuAmCode, SeedConstants.SpecialtySieuAmName, "Siêu âm màu Doppler tim mạch, ổ bụng, tuyến giáp và sản phụ khoa.")
        };

        foreach (var item in specialtiesToSeed)
        {
            var existing = await _dbContext.Specialties.FirstOrDefaultAsync(s => s.SpecialtyCode == item.Code, ct);
            if (existing is null)
            {
                _dbContext.Specialties.Add(new Specialty
                {
                    Id = item.Id,
                    SpecialtyCode = item.Code,
                    SpecialtyName = item.Name,
                    Description = item.Desc,
                    IsActive = true
                });
            }
        }

        // 3. Cấp bậc bác sĩ (Đủ 6 cấp bậc)
        var ranksToSeed = new (Guid Id, RankCode Code, string Name, int Order)[]
        {
            (SeedConstants.RankSpecialistIId, RankCode.SpecialistI, "Bác sĩ Chuyên khoa I", 1),
            (SeedConstants.RankSpecialistIIId, RankCode.SpecialistII, "Bác sĩ Chuyên khoa II", 2),
            (SeedConstants.RankMasterId, RankCode.Master, "Thạc sĩ Bác sĩ", 3),
            (SeedConstants.RankDoctorId, RankCode.Doctor, "Bác sĩ", 4),
            (SeedConstants.RankAssociateProfId, RankCode.AssociateProf, "Phó Giáo sư, Bác sĩ", 5),
            (SeedConstants.RankProfessorId, RankCode.Professor, "Giáo sư, Bác sĩ", 6)
        };

        foreach (var r in ranksToSeed)
        {
            var existing = await _dbContext.DoctorRanks.FirstOrDefaultAsync(x => x.RankCode == r.Code, ct);
            if (existing is null)
            {
                _dbContext.DoctorRanks.Add(new DoctorRank
                {
                    Id = r.Id,
                    RankCode = r.Code,
                    RankName = r.Name,
                    DisplayOrder = r.Order,
                    IsActive = true
                });
            }
        }

        // 4. Phòng chức năng (Rooms)
        // 10 Phòng khám lâm sàng
        var clinicalRooms = new (Guid Id, string Number, string Name)[]
        {
            (SeedConstants.RoomPK01Id, "PK01", "Phòng khám Tiêu hóa 01"),
            (SeedConstants.RoomPK02Id, "PK02", "Phòng khám Tai Mũi Họng 02"),
            (SeedConstants.RoomPK03Id, "PK03", "Phòng khám Da liễu 03"),
            (SeedConstants.RoomPK04Id, "PK04", "Phòng khám Nội tổng quát 04"),
            (SeedConstants.RoomPK05Id, "PK05", "Phòng khám Tim mạch 05"),
            (SeedConstants.RoomPK06Id, "PK06", "Phòng khám Hô hấp 06"),
            (SeedConstants.RoomPK07Id, "PK07", "Phòng khám Thần kinh 07"),
            (SeedConstants.RoomPK08Id, "PK08", "Phòng khám Cơ xương khớp 08"),
            (SeedConstants.RoomPK09Id, "PK09", "Phòng khám Nội tiết 09"),
            (SeedConstants.RoomPK10Id, "PK10", "Phòng khám Nhi 10")
        };

        foreach (var rm in clinicalRooms)
        {
            var existing = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == rm.Number, ct);
            if (existing is null)
            {
                _dbContext.Rooms.Add(new Room
                {
                    Id = rm.Id,
                    RoomNumber = rm.Number,
                    RoomName = rm.Name,
                    RoomType = RoomType.Clinical,
                    DepartmentName = "Khoa Khám bệnh",
                    IsActive = true
                });
            }
        }

        // 6 Phòng Cận lâm sàng (CLS)
        var clsRooms = new (Guid Id, string Number, string Name, RoomType Type, string Dept)[]
        {
            (SeedConstants.RoomXN01Id, SeedConstants.RoomXN01Code, SeedConstants.RoomXN01Name, RoomType.Laboratory, "Khoa Xét nghiệm"),
            (SeedConstants.RoomSA01Id, SeedConstants.RoomSA01Code, SeedConstants.RoomSA01Name, RoomType.Ultrasound, "Khoa Thăm dò chức năng"),
            (SeedConstants.RoomSA02Id, SeedConstants.RoomSA02Code, SeedConstants.RoomSA02Name, RoomType.Ultrasound, "Khoa Thăm dò chức năng"),
            (SeedConstants.RoomXQ01Id, SeedConstants.RoomXQ01Code, SeedConstants.RoomXQ01Name, RoomType.XRay, "Khoa Chẩn đoán hình ảnh"),
            (SeedConstants.RoomXQ02Id, SeedConstants.RoomXQ02Code, SeedConstants.RoomXQ02Name, RoomType.XRay, "Khoa Chẩn đoán hình ảnh"),
            (SeedConstants.RoomNS01Id, SeedConstants.RoomNS01Code, SeedConstants.RoomNS01Name, RoomType.Endoscopy, "Khoa Nội soi")
        };

        foreach (var rm in clsRooms)
        {
            var existing = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == rm.Number, ct);
            if (existing is null)
            {
                _dbContext.Rooms.Add(new Room
                {
                    Id = rm.Id,
                    RoomNumber = rm.Number,
                    RoomName = rm.Name,
                    RoomType = rm.Type,
                    DepartmentName = rm.Dept,
                    IsActive = true
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // 5. Tài khoản phòng CLS (Mỗi phòng CLS đúng một tài khoản)
        var clsAccounts = new (Guid AccountId, Guid RoomId, Guid UserId, string UserName, string Email, string Code)[]
        {
            (SeedConstants.RoomAccount01Id, SeedConstants.RoomXQ01Id, SeedConstants.UserRoomXQ01Id, SeedConstants.UserRoomXQ01UserName, "phong_xq01@smarthealthcare.local", "TK_XQ01"),
            (SeedConstants.RoomAccount02Id, SeedConstants.RoomXQ02Id, SeedConstants.UserRoomXQ02Id, SeedConstants.UserRoomXQ02UserName, "phong_xq02@smarthealthcare.local", "TK_XQ02"),
            (SeedConstants.RoomAccountXN01Id, SeedConstants.RoomXN01Id, SeedConstants.UserRoomXN01Id, SeedConstants.UserRoomXN01UserName, "phong_xn01@smarthealthcare.local", "TK_XN01"),
            (SeedConstants.RoomAccountSA01Id, SeedConstants.RoomSA01Id, SeedConstants.UserRoomSA01Id, SeedConstants.UserRoomSA01UserName, "phong_sa01@smarthealthcare.local", "TK_SA01"),
            (SeedConstants.RoomAccountSA02Id, SeedConstants.RoomSA02Id, SeedConstants.UserRoomSA02Id, SeedConstants.UserRoomSA02UserName, "phong_sa02@smarthealthcare.local", "TK_SA02"),
            (SeedConstants.RoomAccountNS01Id, SeedConstants.RoomNS01Id, SeedConstants.UserRoomNS01Id, SeedConstants.UserRoomNS01UserName, "phong_ns01@smarthealthcare.local", "TK_NS01")
        };

        foreach (var acc in clsAccounts)
        {
            var user = await EnsureUserAsync(acc.UserId, acc.UserName, acc.Email, null, Roles.RoomAccount, defaultPassword);
            var existing = await _dbContext.RoomAccounts.FirstOrDefaultAsync(r => r.RoomId == acc.RoomId, ct);
            if (existing is null)
            {
                _dbContext.RoomAccounts.Add(new RoomAccount
                {
                    Id = acc.AccountId,
                    AppUserId = user.Id,
                    RoomId = acc.RoomId,
                    AccountCode = acc.Code,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 6. Nhân sự & Bác sĩ (Phủ đủ các cấp bậc bác sĩ)
        var doctorsToSeed = new (Guid StaffId, Guid DoctorId, Guid UserId, string UserName, string Email, string Phone, string StaffCode, string FullName, Guid SpecialtyId, Guid RankId, Guid? RoomId, string Bio)[]
        {
            (SeedConstants.StaffDoctor01Id, SeedConstants.Doctor01Id, SeedConstants.UserDoctor01Id, SeedConstants.UserDoctor01UserName, "bacsi01@smarthealthcare.local", "0904000001", "BS01", "BS. CKI Phạm Văn Bác Sĩ", SeedConstants.SpecialtyXQuangId, SeedConstants.RankSpecialistIId, SeedConstants.RoomXQ01Id, "Bác sĩ CKI Chẩn đoán hình ảnh 10 năm kinh nghiệm."),
            (SeedConstants.StaffDoctor02Id, SeedConstants.Doctor02Id, SeedConstants.UserDoctor02Id, SeedConstants.UserDoctor02UserName, "bacsi02@smarthealthcare.local", "0904000002", "BS02", "GS. BS Nguyễn Văn Giáo Sư", SeedConstants.SpecialtyTieuHoaId, SeedConstants.RankProfessorId, SeedConstants.RoomPK01Id, "Chuyên gia đầu ngành tiêu hóa gan mật."),
            (SeedConstants.StaffDoctor03Id, SeedConstants.Doctor03Id, SeedConstants.UserDoctor03Id, SeedConstants.UserDoctor03UserName, "bacsi03@smarthealthcare.local", "0904000003", "BS03", "PGS. BS Trần Thị Phó Giáo Sư", SeedConstants.SpecialtyTaiMuiHongId, SeedConstants.RankAssociateProfId, SeedConstants.RoomPK02Id, "Bác sĩ chuyên khoa Tai Mũi Họng giàu kinh nghiệm."),
            (SeedConstants.StaffDoctor04Id, SeedConstants.Doctor04Id, SeedConstants.UserDoctor04Id, SeedConstants.UserDoctor04UserName, "bacsi04@smarthealthcare.local", "0904000004", "BS04", "BS. CKII Lê Văn Da Liễu", SeedConstants.SpecialtyDaLieuId, SeedConstants.RankSpecialistIIId, SeedConstants.RoomPK03Id, "Bác sĩ CKII chuyên khoa Da Liễu."),
            (SeedConstants.StaffDoctor05Id, SeedConstants.Doctor05Id, SeedConstants.UserDoctor05Id, SeedConstants.UserDoctor05UserName, "bacsi05@smarthealthcare.local", "0904000005", "BS05", "ThS. BS Hoàng Văn Xét Nghiệm", SeedConstants.SpecialtyXetNghiemId, SeedConstants.RankMasterId, SeedConstants.RoomXN01Id, "Thạc sĩ Bác sĩ chuyên khoa Xét nghiệm y học."),
            (SeedConstants.StaffDoctor06Id, SeedConstants.Doctor06Id, SeedConstants.UserDoctor06Id, SeedConstants.UserDoctor06UserName, "bacsi06@smarthealthcare.local", "0904000006", "BS06", "BS. Vũ Văn Nội Soi", SeedConstants.SpecialtyNoiSoiId, SeedConstants.RankDoctorId, SeedConstants.RoomNS01Id, "Bác sĩ điều trị chuyên khoa Nội soi can thiệp.")
        };

        foreach (var doc in doctorsToSeed)
        {
            var user = await EnsureUserAsync(doc.UserId, doc.UserName, doc.Email, doc.Phone, Roles.Doctor, defaultPassword);
            var staff = await _dbContext.Staffs.FirstOrDefaultAsync(s => s.Id == doc.StaffId, ct);
            if (staff is null)
            {
                staff = new Staff
                {
                    Id = doc.StaffId,
                    AppUserId = user.Id,
                    StaffCode = doc.StaffCode,
                    FullName = doc.FullName,
                    StaffType = StaffType.Doctor,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Staffs.Add(staff);
            }

            var doctor = await _dbContext.Doctors.FirstOrDefaultAsync(d => d.Id == doc.DoctorId, ct);
            if (doctor is null)
            {
                doctor = new Doctor
                {
                    Id = doc.DoctorId,
                    StaffId = staff.Id,
                    SpecialtyId = doc.SpecialtyId,
                    DoctorRankId = doc.RankId,
                    DefaultRoomId = doc.RoomId,
                    Biography = doc.Bio,
                    IsActive = true
                };
                _dbContext.Doctors.Add(doctor);
            }
        }

        // 7. Nhân sự khác: Tiếp tân, Điều dưỡng, Admin
        var userReceptionist = await EnsureUserAsync(SeedConstants.UserReceptionist01Id, SeedConstants.UserReceptionist01UserName, "letan01@smarthealthcare.local", "0902000001", Roles.Receptionist, defaultPassword);
        var staffReceptionist = await _dbContext.Staffs.FirstOrDefaultAsync(s => s.Id == SeedConstants.StaffReceptionist01Id, ct);
        if (staffReceptionist is null)
        {
            _dbContext.Staffs.Add(new Staff
            {
                Id = SeedConstants.StaffReceptionist01Id,
                AppUserId = userReceptionist.Id,
                StaffCode = "LT01",
                FullName = "Trần Thị Tiếp Tân",
                StaffType = StaffType.Receptionist,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var userNurse = await EnsureUserAsync(SeedConstants.UserNurse01Id, SeedConstants.UserNurse01UserName, "dieuduong01@smarthealthcare.local", "0903000001", Roles.Nurse, defaultPassword);
        var staffNurse = await _dbContext.Staffs.FirstOrDefaultAsync(s => s.Id == SeedConstants.StaffNurse01Id, ct);
        if (staffNurse is null)
        {
            _dbContext.Staffs.Add(new Staff
            {
                Id = SeedConstants.StaffNurse01Id,
                AppUserId = userNurse.Id,
                StaffCode = "DD01",
                FullName = "Lê Thị Điều Dưỡng",
                StaffType = StaffType.Nurse,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await EnsureUserAsync(SeedConstants.UserAdmin01Id, SeedConstants.UserAdmin01UserName, "admin01@smarthealthcare.local", "0906000001", Roles.Admin, defaultPassword);

        // 8. Bệnh nhân mẫu (Có cả Pending và Verified)
        var userPatient01 = await EnsureUserAsync(SeedConstants.UserPatient01Id, SeedConstants.UserPatient01UserName, "patient01@example.com", "0901000001", Roles.Patient, defaultPassword);

        // Hồ sơ 1: Chính chủ của patient01 (Verified)
        var patient01 = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == SeedConstants.Patient01Id, ct);
        if (patient01 is null)
        {
            patient01 = new Patient
            {
                Id = SeedConstants.Patient01Id,
                OwnerUserId = userPatient01.Id,
                FullName = "Nguyễn Văn Bệnh Nhân",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                NationalId = "001090000001",
                PhoneNumber = "0901000001",
                Address = "123 Đường Sức Khỏe, Quận 1, TP.HCM",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Patients.Add(patient01);
        }

        var rep01 = await _dbContext.PatientRepresentatives.FirstOrDefaultAsync(r => r.AppUserId == userPatient01.Id && r.PatientId == SeedConstants.Patient01Id, ct);
        if (rep01 is null)
        {
            _dbContext.PatientRepresentatives.Add(new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = userPatient01.Id,
                PatientId = SeedConstants.Patient01Id,
                Relationship = Relationship.Self,
                VerificationStatus = VerificationStatus.Verified,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Hồ sơ 2: Con của patient01 (Pending)
        var patient02 = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == SeedConstants.Patient02Id, ct);
        if (patient02 is null)
        {
            patient02 = new Patient
            {
                Id = SeedConstants.Patient02Id,
                OwnerUserId = userPatient01.Id,
                FullName = "Nguyễn Văn Con",
                DateOfBirth = new DateOnly(2020, 5, 15),
                Gender = Gender.Male,
                NationalId = null, // Trẻ em chưa có CCCD
                PhoneNumber = "0901000001",
                Address = "123 Đường Sức Khỏe, Quận 1, TP.HCM",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Patients.Add(patient02);
        }

        var rep02 = await _dbContext.PatientRepresentatives.FirstOrDefaultAsync(r => r.AppUserId == userPatient01.Id && r.PatientId == SeedConstants.Patient02Id, ct);
        if (rep02 is null)
        {
            _dbContext.PatientRepresentatives.Add(new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = userPatient01.Id,
                PatientId = SeedConstants.Patient02Id,
                Relationship = Relationship.Child,
                VerificationStatus = VerificationStatus.Pending,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Hồ sơ 3: Bố của patient01 (Verified)
        var patient03 = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == SeedConstants.Patient03Id, ct);
        if (patient03 is null)
        {
            patient03 = new Patient
            {
                Id = SeedConstants.Patient03Id,
                OwnerUserId = userPatient01.Id,
                FullName = "Nguyễn Văn Bố",
                DateOfBirth = new DateOnly(1960, 10, 20),
                Gender = Gender.Male,
                NationalId = "001060000003",
                PhoneNumber = "0901000003",
                Address = "123 Đường Sức Khỏe, Quận 1, TP.HCM",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Patients.Add(patient03);
        }

        var rep03 = await _dbContext.PatientRepresentatives.FirstOrDefaultAsync(r => r.AppUserId == userPatient01.Id && r.PatientId == SeedConstants.Patient03Id, ct);
        if (rep03 is null)
        {
            _dbContext.PatientRepresentatives.Add(new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = userPatient01.Id,
                PatientId = SeedConstants.Patient03Id,
                Relationship = Relationship.Parent,
                VerificationStatus = VerificationStatus.Verified,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Hồ sơ 4: Bệnh nhân độc lập patient02 (Pending)
        var userPatient02 = await EnsureUserAsync(SeedConstants.UserPatient02Id, SeedConstants.UserPatient02UserName, "patient02@example.com", "0901000002", Roles.Patient, defaultPassword);
        var patient04 = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == SeedConstants.Patient04Id, ct);
        if (patient04 is null)
        {
            patient04 = new Patient
            {
                Id = SeedConstants.Patient04Id,
                OwnerUserId = userPatient02.Id,
                FullName = "Trần Thị Bệnh Nhân",
                DateOfBirth = new DateOnly(1995, 8, 8),
                Gender = Gender.Female,
                NationalId = "001095000004",
                PhoneNumber = "0901000002",
                Address = "456 Đường Y Tế, Quận 3, TP.HCM",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Patients.Add(patient04);
        }

        var rep04 = await _dbContext.PatientRepresentatives.FirstOrDefaultAsync(r => r.AppUserId == userPatient02.Id && r.PatientId == SeedConstants.Patient04Id, ct);
        if (rep04 is null)
        {
            _dbContext.PatientRepresentatives.Add(new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = userPatient02.Id,
                PatientId = SeedConstants.Patient04Id,
                Relationship = Relationship.Self,
                VerificationStatus = VerificationStatus.Pending,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Hoàn tất Seed dữ liệu demo cho module IdentityPatients thành công.");
    }

    private async Task<AppUser> EnsureUserAsync(
        Guid userId,
        string userName,
        string email,
        string? phoneNumber,
        string role,
        string password)
    {
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user is null)
        {
            user = new AppUser
            {
                Id = userId,
                UserName = userName,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHash = PasswordHashing.Hash(password),
                Role = role,
                EmailConfirmed = true,
                PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(phoneNumber),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.AppUsers.Add(user);
            await _dbContext.SaveChangesAsync();
        }
        else if (user.Role != role)
        {
            _logger.LogWarning("Tài khoản seed {User} đã có vai trò {Existing}, không đổi sang {Role}.", userName, user.Role, role);
        }

        return user;
    }
}
