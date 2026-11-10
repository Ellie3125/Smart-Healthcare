using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var defaultPassword = _configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(defaultPassword))
        {
            throw new InvalidOperationException("Cấu hình Seed:DefaultPassword chưa được đặt trong User Secrets hoặc biến môi trường.");
        }

        _logger.LogInformation("Bắt đầu thực hiện Seed dữ liệu cho module IdentityPatients...");

        // 1. Chuyên khoa (Specialty) - Unique Code "XQUANG"
        var specialty = await _dbContext.Specialties.FirstOrDefaultAsync(s => s.SpecialtyCode == SeedConstants.SpecialtyXQuangCode, ct);
        if (specialty is null)
        {
            specialty = new Specialty
            {
                Id = SeedConstants.SpecialtyXQuangId,
                SpecialtyCode = SeedConstants.SpecialtyXQuangCode,
                SpecialtyName = SeedConstants.SpecialtyXQuangName,
                Description = "Chẩn đoán hình ảnh kỹ thuật số, X-Quang can thiệp và kiểm tra định kỳ.",
                IsActive = true
            };
            _dbContext.Specialties.Add(specialty);
        }

        // 2. Cấp bậc bác sĩ (DoctorRank)
        var rankSpecialistI = await _dbContext.DoctorRanks.FirstOrDefaultAsync(r => r.RankCode == RankCode.SpecialistI, ct);
        if (rankSpecialistI is null)
        {
            rankSpecialistI = new DoctorRank
            {
                Id = SeedConstants.RankSpecialistIId,
                RankCode = RankCode.SpecialistI,
                RankName = "Bác sĩ Chuyên khoa I",
                DisplayOrder = 1,
                IsActive = true
            };
            _dbContext.DoctorRanks.Add(rankSpecialistI);
        }

        var rankDoctor = await _dbContext.DoctorRanks.FirstOrDefaultAsync(r => r.RankCode == RankCode.Doctor, ct);
        if (rankDoctor is null)
        {
            rankDoctor = new DoctorRank
            {
                Id = SeedConstants.RankDoctorId,
                RankCode = RankCode.Doctor,
                RankName = "Bác sĩ",
                DisplayOrder = 2,
                IsActive = true
            };
            _dbContext.DoctorRanks.Add(rankDoctor);
        }

        // 3. Phòng chức năng (Room)
        var room01 = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == SeedConstants.RoomXQ01Code, ct);
        if (room01 is null)
        {
            room01 = new Room
            {
                Id = SeedConstants.RoomXQ01Id,
                RoomNumber = SeedConstants.RoomXQ01Code,
                RoomName = SeedConstants.RoomXQ01Name,
                RoomType = RoomType.XRay,
                DepartmentName = "Khoa Chẩn đoán hình ảnh",
                IsActive = true
            };
            _dbContext.Rooms.Add(room01);
        }

        var room02 = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == SeedConstants.RoomXQ02Code, ct);
        if (room02 is null)
        {
            room02 = new Room
            {
                Id = SeedConstants.RoomXQ02Id,
                RoomNumber = SeedConstants.RoomXQ02Code,
                RoomName = SeedConstants.RoomXQ02Name,
                RoomType = RoomType.XRay,
                DepartmentName = "Khoa Chẩn đoán hình ảnh",
                IsActive = true
            };
            _dbContext.Rooms.Add(room02);
        }

        await _dbContext.SaveChangesAsync(ct);

        // 4. Tài khoản người dùng (AppUser) và các thực thể liên kết

        // 4.1 Bệnh nhân: patient01
        var userPatient = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserPatient01UserName, ct);
        if (userPatient is null)
        {
            userPatient = new AppUser
            {
                Id = SeedConstants.UserPatient01Id,
                UserName = SeedConstants.UserPatient01UserName,
                Email = "patient01@example.com",
                PhoneNumber = "0901000001",
                Role = Roles.Patient,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userPatient.PasswordHash = _passwordHasher.HashPassword(userPatient, defaultPassword);
            _dbContext.AppUsers.Add(userPatient);

            var patient = new Patient
            {
                Id = SeedConstants.Patient01Id,
                OwnerUserId = userPatient.Id,
                FullName = "Nguyễn Văn Bệnh Nhân",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                NationalId = "001090000001",
                PhoneNumber = "0901000001",
                Address = "123 Đường Sức Khỏe, Quận 1, TP.HCM",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Patients.Add(patient);
        }

        // 4.2 Tiếp tân: letan01
        var userReceptionist = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserReceptionist01UserName, ct);
        if (userReceptionist is null)
        {
            userReceptionist = new AppUser
            {
                Id = SeedConstants.UserReceptionist01Id,
                UserName = SeedConstants.UserReceptionist01UserName,
                Email = "letan01@smarthealthcare.local",
                PhoneNumber = "0902000001",
                Role = Roles.Receptionist,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userReceptionist.PasswordHash = _passwordHasher.HashPassword(userReceptionist, defaultPassword);
            _dbContext.AppUsers.Add(userReceptionist);

            var staff = new Staff
            {
                Id = SeedConstants.StaffReceptionist01Id,
                AppUserId = userReceptionist.Id,
                StaffCode = "LT01",
                FullName = "Trần Thị Tiếp Tân",
                StaffType = StaffType.Receptionist,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Staffs.Add(staff);
        }

        // 4.3 Điều dưỡng: dieuduong01
        var userNurse = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserNurse01UserName, ct);
        if (userNurse is null)
        {
            userNurse = new AppUser
            {
                Id = SeedConstants.UserNurse01Id,
                UserName = SeedConstants.UserNurse01UserName,
                Email = "dieuduong01@smarthealthcare.local",
                PhoneNumber = "0903000001",
                Role = Roles.Nurse,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userNurse.PasswordHash = _passwordHasher.HashPassword(userNurse, defaultPassword);
            _dbContext.AppUsers.Add(userNurse);

            var staff = new Staff
            {
                Id = SeedConstants.StaffNurse01Id,
                AppUserId = userNurse.Id,
                StaffCode = "DD01",
                FullName = "Lê Thị Điều Dưỡng",
                StaffType = StaffType.Nurse,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Staffs.Add(staff);
        }

        // 4.4 Bác sĩ: bacsi01 (gắn DoctorId = SeedConstants.Doctor01Id)
        var userDoctor = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserDoctor01UserName, ct);
        if (userDoctor is null)
        {
            userDoctor = new AppUser
            {
                Id = SeedConstants.UserDoctor01Id,
                UserName = SeedConstants.UserDoctor01UserName,
                Email = "bacsi01@smarthealthcare.local",
                PhoneNumber = "0904000001",
                Role = Roles.Doctor,
                DoctorId = SeedConstants.Doctor01Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userDoctor.PasswordHash = _passwordHasher.HashPassword(userDoctor, defaultPassword);
            _dbContext.AppUsers.Add(userDoctor);

            var staff = new Staff
            {
                Id = SeedConstants.StaffDoctor01Id,
                AppUserId = userDoctor.Id,
                StaffCode = "BS01",
                FullName = "BS. CKI Phạm Văn Bác Sĩ",
                StaffType = StaffType.Doctor,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Staffs.Add(staff);

            var doctor = new Doctor
            {
                Id = SeedConstants.Doctor01Id,
                StaffId = staff.Id,
                SpecialtyId = SeedConstants.SpecialtyXQuangId,
                DoctorRankId = SeedConstants.RankSpecialistIId,
                DefaultRoomId = SeedConstants.RoomXQ01Id,
                Biography = "Bác sĩ CKI Chẩn đoán hình ảnh với hơn 10 năm kinh nghiệm.",
                IsActive = true
            };
            _dbContext.Doctors.Add(doctor);
        }

        // 4.5 Phòng X-quang 01: phong_xq01 (Role: RoomAccount, gắn RoomId = SeedConstants.RoomXQ01Id)
        var userRoom01 = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserRoomXQ01UserName, ct);
        if (userRoom01 is null)
        {
            userRoom01 = new AppUser
            {
                Id = SeedConstants.UserRoomXQ01Id,
                UserName = SeedConstants.UserRoomXQ01UserName,
                Email = "phong_xq01@smarthealthcare.local",
                PhoneNumber = "0905000001",
                Role = Roles.RoomAccount,
                RoomId = SeedConstants.RoomXQ01Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userRoom01.PasswordHash = _passwordHasher.HashPassword(userRoom01, defaultPassword);
            _dbContext.AppUsers.Add(userRoom01);

            var roomAccount = new RoomAccount
            {
                Id = SeedConstants.RoomAccount01Id,
                AppUserId = userRoom01.Id,
                RoomId = SeedConstants.RoomXQ01Id,
                AccountCode = "TK_XQ01",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.RoomAccounts.Add(roomAccount);
        }

        // 4.6 Phòng X-quang 02: phong_xq02 (Role: RoomAccount, gắn RoomId = SeedConstants.RoomXQ02Id)
        var userRoom02 = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserRoomXQ02UserName, ct);
        if (userRoom02 is null)
        {
            userRoom02 = new AppUser
            {
                Id = SeedConstants.UserRoomXQ02Id,
                UserName = SeedConstants.UserRoomXQ02UserName,
                Email = "phong_xq02@smarthealthcare.local",
                PhoneNumber = "0905000002",
                Role = Roles.RoomAccount,
                RoomId = SeedConstants.RoomXQ02Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userRoom02.PasswordHash = _passwordHasher.HashPassword(userRoom02, defaultPassword);
            _dbContext.AppUsers.Add(userRoom02);

            var roomAccount = new RoomAccount
            {
                Id = SeedConstants.RoomAccount02Id,
                AppUserId = userRoom02.Id,
                RoomId = SeedConstants.RoomXQ02Id,
                AccountCode = "TK_XQ02",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.RoomAccounts.Add(roomAccount);
        }

        // 4.7 Quản trị hệ thống kỹ thuật: admin01
        var userAdmin = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserName == SeedConstants.UserAdmin01UserName, ct);
        if (userAdmin is null)
        {
            userAdmin = new AppUser
            {
                Id = SeedConstants.UserAdmin01Id,
                UserName = SeedConstants.UserAdmin01UserName,
                Email = "admin01@smarthealthcare.local",
                PhoneNumber = "0906000001",
                Role = Roles.Admin,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            userAdmin.PasswordHash = _passwordHasher.HashPassword(userAdmin, defaultPassword);
            _dbContext.AppUsers.Add(userAdmin);
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Hoàn tất Seed dữ liệu cho module IdentityPatients thành công.");
    }
}

