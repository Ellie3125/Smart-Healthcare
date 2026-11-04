using IdentityPatients.Contracts.Constants;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityPatients.Infrastructure.Seed;

public sealed class IdentitySeeder(
    IdentityPatientsDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var password = configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Cấu hình 'Seed:DefaultPassword' không được tìm thấy hoặc rỗng. " +
                "Vui lòng thiết lập qua User Secrets: dotnet user-secrets set \"Seed:DefaultPassword\" \"<Mật-khẩu>\" " +
                "hoặc qua biến môi trường Seed__DefaultPassword.");
        }

        logger.LogInformation("Bắt đầu thực hiện seed dữ liệu Identity...");

        // 1. Chuyên khoa: Nội tổng quát (mã cố định để chatbot/Scheduling tham chiếu)
        if (!await dbContext.Specialties.AnyAsync(s => s.Id == SeedConstants.NoiTongQuatSpecialtyId, ct))
        {
            dbContext.Specialties.Add(new Specialty
            {
                Id = SeedConstants.NoiTongQuatSpecialtyId,
                Code = SeedConstants.NoiTongQuatCode,
                Name = SeedConstants.NoiTongQuatName,
                IsActive = true
            });
            logger.LogInformation("Seed Specialty: {Code}", SeedConstants.NoiTongQuatCode);
        }

        // 2. Cấp bậc bác sĩ (2 cấp bậc)
        if (!await dbContext.DoctorRanks.AnyAsync(r => r.Id == SeedConstants.RankBacSiChuyenKhoaId, ct))
        {
            dbContext.DoctorRanks.Add(new DoctorRank
            {
                Id = SeedConstants.RankBacSiChuyenKhoaId,
                Code = SeedConstants.RankBacSiChuyenKhoaCode,
                Name = SeedConstants.RankBacSiChuyenKhoaName,
                SortOrder = 1,
                IsActive = true
            });
        }

        if (!await dbContext.DoctorRanks.AnyAsync(r => r.Id == SeedConstants.RankTienSiBacSiId, ct))
        {
            dbContext.DoctorRanks.Add(new DoctorRank
            {
                Id = SeedConstants.RankTienSiBacSiId,
                Code = SeedConstants.RankTienSiBacSiCode,
                Name = SeedConstants.RankTienSiBacSiName,
                SortOrder = 2,
                IsActive = true
            });
        }

        // 3. Phòng khám & Phòng CLS (PK01, XQ01, XQ02)
        if (!await dbContext.Rooms.AnyAsync(r => r.Id == SeedConstants.RoomPhongKham01Id, ct))
        {
            dbContext.Rooms.Add(new Room
            {
                Id = SeedConstants.RoomPhongKham01Id,
                Code = SeedConstants.RoomPhongKham01Code,
                Name = SeedConstants.RoomPhongKham01Name,
                RoomType = "Exam",
                IsActive = true
            });
        }

        if (!await dbContext.Rooms.AnyAsync(r => r.Id == SeedConstants.RoomXQ01Id, ct))
        {
            dbContext.Rooms.Add(new Room
            {
                Id = SeedConstants.RoomXQ01Id,
                Code = SeedConstants.RoomXQ01Code,
                Name = SeedConstants.RoomXQ01Name,
                RoomType = "CLS",
                IsActive = true
            });
        }

        if (!await dbContext.Rooms.AnyAsync(r => r.Id == SeedConstants.RoomXQ02Id, ct))
        {
            dbContext.Rooms.Add(new Room
            {
                Id = SeedConstants.RoomXQ02Id,
                Code = SeedConstants.RoomXQ02Code,
                Name = SeedConstants.RoomXQ02Name,
                RoomType = "CLS",
                IsActive = true
            });
        }

        // 4. Bác sĩ (Doctor)
        if (!await dbContext.Doctors.AnyAsync(d => d.Id == SeedConstants.Doctor01Id, ct))
        {
            dbContext.Doctors.Add(new Doctor
            {
                Id = SeedConstants.Doctor01Id,
                FullName = SeedConstants.Doctor01FullName,
                SpecialtyId = SeedConstants.NoiTongQuatSpecialtyId,
                DoctorRankId = SeedConstants.RankBacSiChuyenKhoaId,
                DefaultRoomId = SeedConstants.RoomPhongKham01Id,
                IsActive = true
            });
            logger.LogInformation("Seed Doctor: {Name}", SeedConstants.Doctor01FullName);
        }

        // Lưu các entity cha trước để bảo đảm toàn vẹn tham chiếu
        await dbContext.SaveChangesAsync(ct);

        // 5. Danh sách tài khoản người dùng (AppUser)
        var usersToSeed = new (Guid Id, string UserName, string Role, Guid? DoctorId, Guid? RoomId)[]
        {
            (SeedConstants.UserPatient01Id, "patient01", Roles.Patient, null, null),
            (SeedConstants.UserLetan01Id, "letan01", Roles.Receptionist, null, null),
            (SeedConstants.UserDieuduong01Id, "dieuduong01", Roles.Nurse, null, null),
            (SeedConstants.UserBacsi01Id, "bacsi01", Roles.Doctor, SeedConstants.Doctor01Id, null),
            (SeedConstants.UserPhongXQ01Id, "phong_xq01", Roles.RoomAccount, null, SeedConstants.RoomXQ01Id),
            (SeedConstants.UserPhongXQ02Id, "phong_xq02", Roles.RoomAccount, null, SeedConstants.RoomXQ02Id),
            (SeedConstants.UserAdmin01Id, "admin01", Roles.Admin, null, null)
        };

        foreach (var (id, userName, role, docId, roomId) in usersToSeed)
        {
            if (!await dbContext.AppUsers.AnyAsync(u => u.Id == id, ct))
            {
                var user = new AppUser
                {
                    Id = id,
                    UserName = userName,
                    Role = role,
                    DoctorId = docId,
                    RoomId = roomId,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                dbContext.AppUsers.Add(user);
                logger.LogInformation("Seed AppUser: {UserName} (Role: {Role})", userName, role);
            }
        }

        // 6. Hồ sơ bệnh nhân mẫu (gắn với patient01)
        if (!await dbContext.Patients.AnyAsync(p => p.Id == SeedConstants.Patient01Id, ct))
        {
            dbContext.Patients.Add(new Patient
            {
                Id = SeedConstants.Patient01Id,
                OwnerUserId = SeedConstants.UserPatient01Id,
                FullName = "Nguyễn Văn Bệnh Nhân",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Male,
                PhoneNumber = "0901234567",
                Address = "Hà Nội"
            });
            logger.LogInformation("Seed Patient: {Id}", SeedConstants.Patient01Id);
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Hoàn tất seed dữ liệu Identity thành công.");
    }
}

