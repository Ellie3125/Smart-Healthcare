using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class StaffAdminService(
    IdentityPatientsDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IStaffAdminService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    // --- Specialties ---

    public async Task<ServiceResult<IReadOnlyList<SpecialtyAdminDto>>> GetSpecialtiesAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.Specialties
            .AsNoTracking()
            .OrderBy(s => s.SpecialtyCode)
            .Select(s => new SpecialtyAdminDto(s.Id, s.SpecialtyCode, s.SpecialtyName, s.Description, s.IsActive))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<SpecialtyAdminDto>>.Success(list);
    }

    public async Task<ServiceResult<SpecialtyAdminDto>> CreateSpecialtyAsync(CreateSpecialtyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<SpecialtyAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Mã và tên chuyên khoa không được để trống.");
        }

        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = request.Code.Trim().ToUpperInvariant(),
            SpecialtyName = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true
        };

        _dbContext.Specialties.Add(specialty);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "SpecialtyCode"))
            {
                return ServiceResult<SpecialtyAdminDto>.Failure(409, "Trùng mã chuyên khoa", $"Mã chuyên khoa '{request.Code}' đã tồn tại.");
            }
            return ServiceResult<SpecialtyAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo chuyên khoa do xung đột dữ liệu.");
        }

        return ServiceResult<SpecialtyAdminDto>.Success(
            new SpecialtyAdminDto(specialty.Id, specialty.SpecialtyCode, specialty.SpecialtyName, specialty.Description, specialty.IsActive), 201);
    }

    public async Task<ServiceResult<SpecialtyAdminDto>> UpdateSpecialtyAsync(Guid id, UpdateSpecialtyRequest request, CancellationToken ct = default)
    {
        var specialty = await _dbContext.Specialties.FindAsync([id], ct);
        if (specialty is null)
        {
            return ServiceResult<SpecialtyAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy chuyên khoa.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<SpecialtyAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên chuyên khoa không được để trống.");
        }

        // Quy tắc: SpecialtyCode KHÔNG cho đổi sau khi tạo (chatbot tham chiếu)
        specialty.SpecialtyName = request.Name.Trim();
        specialty.Description = request.Description?.Trim();
        specialty.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return ServiceResult<SpecialtyAdminDto>.Success(
            new SpecialtyAdminDto(specialty.Id, specialty.SpecialtyCode, specialty.SpecialtyName, specialty.Description, specialty.IsActive));
    }

    // --- Doctor Ranks ---

    public async Task<ServiceResult<IReadOnlyList<DoctorRankAdminDto>>> GetDoctorRanksAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.DoctorRanks
            .AsNoTracking()
            .OrderBy(r => r.DisplayOrder)
            .Select(r => new DoctorRankAdminDto(r.Id, r.RankCode.ToString(), r.RankName, r.DisplayOrder, r.IsActive))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<DoctorRankAdminDto>>.Success(list);
    }

    public async Task<ServiceResult<DoctorRankAdminDto>> CreateDoctorRankAsync(CreateDoctorRankRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<RankCode>(request.Code, ignoreCase: true, out var rankCode))
        {
            return ServiceResult<DoctorRankAdminDto>.Failure(400, "Mã cấp bậc không hợp lệ", "Mã cấp bậc hợp lệ: SpecialistI, SpecialistII, Master, Doctor, AssociateProf, Professor.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<DoctorRankAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên cấp bậc không được để trống.");
        }

        var rank = new DoctorRank
        {
            Id = Guid.NewGuid(),
            RankCode = rankCode,
            RankName = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = true
        };

        _dbContext.DoctorRanks.Add(rank);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "RankCode"))
            {
                return ServiceResult<DoctorRankAdminDto>.Failure(409, "Trùng mã cấp bậc", $"Cấp bậc '{rankCode}' đã tồn tại.");
            }
            return ServiceResult<DoctorRankAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo cấp bậc do xung đột.");
        }

        return ServiceResult<DoctorRankAdminDto>.Success(
            new DoctorRankAdminDto(rank.Id, rank.RankCode.ToString(), rank.RankName, rank.DisplayOrder, rank.IsActive), 201);
    }

    public async Task<ServiceResult<DoctorRankAdminDto>> UpdateDoctorRankAsync(Guid id, UpdateDoctorRankRequest request, CancellationToken ct = default)
    {
        var rank = await _dbContext.DoctorRanks.FindAsync([id], ct);
        if (rank is null)
        {
            return ServiceResult<DoctorRankAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy cấp bậc.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<DoctorRankAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên cấp bậc không được để trống.");
        }

        rank.RankName = request.Name.Trim();
        rank.DisplayOrder = request.DisplayOrder;
        rank.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return ServiceResult<DoctorRankAdminDto>.Success(
            new DoctorRankAdminDto(rank.Id, rank.RankCode.ToString(), rank.RankName, rank.DisplayOrder, rank.IsActive));
    }

    // --- Rooms ---

    public async Task<ServiceResult<IReadOnlyList<RoomAdminDto>>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _dbContext.Rooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(roomType) && Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var parsedType))
        {
            query = query.Where(r => r.RoomType == parsedType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        var list = await query
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomAdminDto(r.Id, r.RoomNumber, r.RoomName, r.RoomType.ToString(), r.DepartmentName, r.IsActive))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<RoomAdminDto>>.Success(list);
    }

    public async Task<ServiceResult<RoomAdminDto>> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RoomNumber) || string.IsNullOrWhiteSpace(request.RoomName))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Số phòng và tên phòng không được để trống.");
        }

        if (!Enum.TryParse<RoomType>(request.RoomType, ignoreCase: true, out var roomType))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Loại phòng không hợp lệ", "Loại phòng hợp lệ: Clinical, Laboratory, XRay, Ultrasound, Endoscopy.");
        }

        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = request.RoomNumber.Trim().ToUpperInvariant(),
            RoomName = request.RoomName.Trim(),
            RoomType = roomType,
            DepartmentName = request.DepartmentName.Trim(),
            IsActive = true
        };

        _dbContext.Rooms.Add(room);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "RoomNumber"))
            {
                return ServiceResult<RoomAdminDto>.Failure(409, "Trùng số phòng", $"Số phòng '{request.RoomNumber}' đã tồn tại.");
            }
            return ServiceResult<RoomAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo phòng do xung đột dữ liệu.");
        }

        return ServiceResult<RoomAdminDto>.Success(
            new RoomAdminDto(room.Id, room.RoomNumber, room.RoomName, room.RoomType.ToString(), room.DepartmentName, room.IsActive), 201);
    }

    public async Task<ServiceResult<RoomAdminDto>> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms.FindAsync([id], ct);
        if (room is null)
        {
            return ServiceResult<RoomAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy phòng.");
        }

        if (string.IsNullOrWhiteSpace(request.RoomName))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên phòng không được để trống.");
        }

        if (!Enum.TryParse<RoomType>(request.RoomType, ignoreCase: true, out var roomType))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Loại phòng không hợp lệ", "Loại phòng hợp lệ: Clinical, Laboratory, XRay, Ultrasound, Endoscopy.");
        }

        room.RoomName = request.RoomName.Trim();
        room.RoomType = roomType;
        room.DepartmentName = request.DepartmentName.Trim();
        room.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return ServiceResult<RoomAdminDto>.Success(
            new RoomAdminDto(room.Id, room.RoomNumber, room.RoomName, room.RoomType.ToString(), room.DepartmentName, room.IsActive));
    }

    // --- Staff ---

    public async Task<ServiceResult<IReadOnlyList<StaffAdminDto>>> GetStaffListAsync(string? staffType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = from s in _dbContext.Staffs.AsNoTracking()
                    join u in _dbContext.Users.AsNoTracking() on s.AppUserId equals u.Id
                    select new { Staff = s, User = u };

        if (!string.IsNullOrWhiteSpace(staffType) && Enum.TryParse<StaffType>(staffType, ignoreCase: true, out var parsedType))
        {
            query = query.Where(x => x.Staff.StaffType == parsedType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.Staff.IsActive == isActive.Value);
        }

        var list = await query
            .OrderBy(x => x.Staff.StaffCode)
            .Select(x => new StaffAdminDto(
                x.Staff.Id,
                x.Staff.AppUserId,
                x.Staff.StaffCode,
                x.Staff.FullName,
                x.Staff.StaffType.ToString(),
                x.User.UserName ?? string.Empty,
                x.User.Email ?? string.Empty,
                x.User.PhoneNumber,
                x.Staff.IsActive,
                x.Staff.CreatedAtUtc))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<StaffAdminDto>>.Success(list);
    }

    public async Task<ServiceResult<StaffAdminDto>> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên đăng nhập và mật khẩu không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.StaffCode) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Mã nhân viên và họ tên không được để trống.");
        }

        if (!Enum.TryParse<StaffType>(request.StaffType, ignoreCase: true, out var staffType))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Loại nhân sự không hợp lệ", "Loại nhân sự hợp lệ: Doctor, Nurse, Receptionist, Technician.");
        }

        var targetRole = staffType switch
        {
            StaffType.Doctor => Roles.Doctor,
            StaffType.Nurse => Roles.Nurse,
            StaffType.Receptionist => Roles.Receptionist,
            _ => Roles.Doctor // Fallback nếu chưa có role riêng cho Technician
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EmailConfirmed = true,
            PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(request.PhoneNumber),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return ServiceResult<StaffAdminDto>.Failure(400, "Tạo tài khoản thất bại", errors);
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, targetRole);
        if (!addRoleResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<StaffAdminDto>.Failure(400, "Gán quyền thất bại", "Không thể gán vai trò nhân sự.");
        }

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = user.Id,
            StaffCode = request.StaffCode.Trim().ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            StaffType = staffType,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Staffs.Add(staff);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            if (IsUniqueViolation(ex, "StaffCode"))
            {
                return ServiceResult<StaffAdminDto>.Failure(409, "Trùng mã nhân viên", $"Mã nhân viên '{request.StaffCode}' đã tồn tại.");
            }
            if (IsUniqueViolation(ex, "AppUserId"))
            {
                return ServiceResult<StaffAdminDto>.Failure(409, "Xung đột tài khoản", "Tài khoản người dùng đã được liên kết với nhân sự khác.");
            }
            return ServiceResult<StaffAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo nhân sự do xung đột dữ liệu.");
        }

        var dto = new StaffAdminDto(
            staff.Id,
            staff.AppUserId,
            staff.StaffCode,
            staff.FullName,
            staff.StaffType.ToString(),
            user.UserName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            staff.IsActive,
            staff.CreatedAtUtc);

        return ServiceResult<StaffAdminDto>.Success(dto, 201);
    }

    public async Task<ServiceResult<StaffAdminDto>> UpdateStaffAsync(Guid id, UpdateStaffRequest request, CancellationToken ct = default)
    {
        var staff = await _dbContext.Staffs.FindAsync([id], ct);
        if (staff is null)
        {
            return ServiceResult<StaffAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Họ tên không được để trống.");
        }

        staff.FullName = request.FullName.Trim();
        staff.IsActive = request.IsActive;

        var user = await _userManager.FindByIdAsync(staff.AppUserId.ToString());
        if (user != null)
        {
            user.IsActive = request.IsActive;
            if (!request.IsActive)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            await _userManager.UpdateAsync(user);
        }

        await _dbContext.SaveChangesAsync(ct);

        var dto = new StaffAdminDto(
            staff.Id,
            staff.AppUserId,
            staff.StaffCode,
            staff.FullName,
            staff.StaffType.ToString(),
            user?.UserName ?? string.Empty,
            user?.Email ?? string.Empty,
            user?.PhoneNumber,
            staff.IsActive,
            staff.CreatedAtUtc);

        return ServiceResult<StaffAdminDto>.Success(dto);
    }

    public async Task<ServiceResult<StaffAdminDto>> ToggleStaffActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var staff = await _dbContext.Staffs.FindAsync([id], ct);
        if (staff is null)
        {
            return ServiceResult<StaffAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự.");
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        staff.IsActive = isActive;
        var user = await _userManager.FindByIdAsync(staff.AppUserId.ToString());
        if (user != null)
        {
            user.IsActive = isActive;
            if (!isActive)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            await _userManager.UpdateAsync(user);
        }

        if (staff.StaffType == StaffType.Doctor)
        {
            var doctor = await _dbContext.Doctors.FirstOrDefaultAsync(d => d.StaffId == staff.Id, ct);
            if (doctor != null)
            {
                doctor.IsActive = isActive;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = new StaffAdminDto(
            staff.Id,
            staff.AppUserId,
            staff.StaffCode,
            staff.FullName,
            staff.StaffType.ToString(),
            user?.UserName ?? string.Empty,
            user?.Email ?? string.Empty,
            user?.PhoneNumber,
            staff.IsActive,
            staff.CreatedAtUtc);

        return ServiceResult<StaffAdminDto>.Success(dto);
    }

    // --- Doctors ---

    public async Task<ServiceResult<IReadOnlyList<DoctorAdminDto>>> GetDoctorsAsync(
        Guid? specialtyId = null,
        Guid? rankId = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = from d in _dbContext.Doctors.AsNoTracking()
                    join s in _dbContext.Staffs.AsNoTracking() on d.StaffId equals s.Id
                    join spec in _dbContext.Specialties.AsNoTracking() on d.SpecialtyId equals spec.Id
                    join r in _dbContext.DoctorRanks.AsNoTracking() on d.DoctorRankId equals r.Id
                    select new { Doctor = d, Staff = s, Specialty = spec, Rank = r };

        if (specialtyId.HasValue)
        {
            query = query.Where(x => x.Specialty.Id == specialtyId.Value);
        }

        if (rankId.HasValue)
        {
            query = query.Where(x => x.Rank.Id == rankId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.Doctor.IsActive == isActive.Value);
        }

        var list = await query
            .OrderBy(x => x.Staff.StaffCode)
            .Select(x => new DoctorAdminDto(
                x.Doctor.Id,
                x.Doctor.StaffId,
                x.Staff.FullName,
                x.Staff.StaffCode,
                x.Specialty.Id,
                x.Specialty.SpecialtyName,
                x.Rank.Id,
                x.Rank.RankName,
                x.Doctor.DefaultRoomId,
                x.Doctor.Biography,
                x.Doctor.IsActive))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<DoctorAdminDto>>.Success(list);
    }

    public async Task<ServiceResult<DoctorAdminDto>> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct = default)
    {
        var staff = await _dbContext.Staffs.FindAsync([request.StaffId], ct);
        if (staff is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự tương ứng.");
        }

        if (staff.StaffType != StaffType.Doctor)
        {
            return ServiceResult<DoctorAdminDto>.Failure(400, "Sai loại nhân sự", "Chỉ có thể tạo thông tin bác sĩ cho nhân viên có loại StaffType = Doctor.");
        }

        var specialty = await _dbContext.Specialties.FindAsync([request.SpecialtyId], ct);
        if (specialty is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy chuyên khoa.");
        }

        var rank = await _dbContext.DoctorRanks.FindAsync([request.DoctorRankId], ct);
        if (rank is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy cấp bậc bác sĩ.");
        }

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            SpecialtyId = request.SpecialtyId,
            DoctorRankId = request.DoctorRankId,
            DefaultRoomId = request.DefaultRoomId,
            Biography = request.Biography?.Trim(),
            IsActive = true
        };

        _dbContext.Doctors.Add(doctor);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "StaffId"))
            {
                return ServiceResult<DoctorAdminDto>.Failure(409, "Bác sĩ đã tồn tại", "Nhân sự này đã có thông tin bác sĩ trong hệ thống.");
            }
            return ServiceResult<DoctorAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo bác sĩ do xung đột dữ liệu.");
        }

        var dto = new DoctorAdminDto(
            doctor.Id,
            doctor.StaffId,
            staff.FullName,
            staff.StaffCode,
            specialty.Id,
            specialty.SpecialtyName,
            rank.Id,
            rank.RankName,
            doctor.DefaultRoomId,
            doctor.Biography,
            doctor.IsActive);

        return ServiceResult<DoctorAdminDto>.Success(dto, 201);
    }

    public async Task<ServiceResult<DoctorAdminDto>> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors.FindAsync([id], ct);
        if (doctor is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy bác sĩ.");
        }

        var specialty = await _dbContext.Specialties.FindAsync([request.SpecialtyId], ct);
        if (specialty is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy chuyên khoa.");
        }

        var rank = await _dbContext.DoctorRanks.FindAsync([request.DoctorRankId], ct);
        if (rank is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy cấp bậc bác sĩ.");
        }

        doctor.SpecialtyId = request.SpecialtyId;
        doctor.DoctorRankId = request.DoctorRankId;
        doctor.DefaultRoomId = request.DefaultRoomId;
        doctor.Biography = request.Biography?.Trim();
        doctor.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        var staff = await _dbContext.Staffs.FindAsync([doctor.StaffId], ct);

        var dto = new DoctorAdminDto(
            doctor.Id,
            doctor.StaffId,
            staff?.FullName ?? string.Empty,
            staff?.StaffCode ?? string.Empty,
            specialty.Id,
            specialty.SpecialtyName,
            rank.Id,
            rank.RankName,
            doctor.DefaultRoomId,
            doctor.Biography,
            doctor.IsActive);

        return ServiceResult<DoctorAdminDto>.Success(dto);
    }

    public async Task<ServiceResult<DoctorAdminDto>> ToggleDoctorActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors.FindAsync([id], ct);
        if (doctor is null)
        {
            return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy bác sĩ.");
        }

        doctor.IsActive = isActive;
        await _dbContext.SaveChangesAsync(ct);

        var staff = await _dbContext.Staffs.FindAsync([doctor.StaffId], ct);
        var specialty = await _dbContext.Specialties.FindAsync([doctor.SpecialtyId], ct);
        var rank = await _dbContext.DoctorRanks.FindAsync([doctor.DoctorRankId], ct);

        var dto = new DoctorAdminDto(
            doctor.Id,
            doctor.StaffId,
            staff?.FullName ?? string.Empty,
            staff?.StaffCode ?? string.Empty,
            specialty?.Id ?? Guid.Empty,
            specialty?.SpecialtyName ?? string.Empty,
            rank?.Id ?? Guid.Empty,
            rank?.RankName ?? string.Empty,
            doctor.DefaultRoomId,
            doctor.Biography,
            doctor.IsActive);

        return ServiceResult<DoctorAdminDto>.Success(dto);
    }

    // --- Shift Assignments ---

    public async Task<ServiceResult<IReadOnlyList<ShiftAssignmentDto>>> GetShiftAssignmentsAsync(
        DateOnly? date = null,
        Guid? roomId = null,
        Guid? staffId = null,
        CancellationToken ct = default)
    {
        var query = from a in _dbContext.StaffShiftAssignments.AsNoTracking()
                    join s in _dbContext.Staffs.AsNoTracking() on a.StaffId equals s.Id
                    join r in _dbContext.Rooms.AsNoTracking() on a.RoomId equals r.Id
                    select new { Assignment = a, Staff = s, Room = r };

        if (date.HasValue)
        {
            query = query.Where(x => x.Assignment.ShiftDate == date.Value);
        }

        if (roomId.HasValue)
        {
            query = query.Where(x => x.Assignment.RoomId == roomId.Value);
        }

        if (staffId.HasValue)
        {
            query = query.Where(x => x.Assignment.StaffId == staffId.Value);
        }

        var list = await query
            .OrderBy(x => x.Assignment.ShiftDate)
            .ThenBy(x => x.Room.RoomNumber)
            .Select(x => new ShiftAssignmentDto(
                x.Assignment.Id,
                x.Staff.Id,
                x.Staff.FullName,
                x.Staff.StaffCode,
                x.Room.Id,
                x.Room.RoomNumber,
                x.Room.RoomName,
                x.Assignment.ShiftDate,
                x.Assignment.ShiftName.ToString(),
                x.Assignment.StartsAtUtc,
                x.Assignment.EndsAtUtc,
                x.Assignment.IsShiftLeader))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ShiftAssignmentDto>>.Success(list);
    }

    public async Task<ServiceResult<ShiftAssignmentDto>> CreateShiftAssignmentAsync(
        CreateShiftAssignmentRequest request,
        CancellationToken ct = default)
    {
        var staff = await _dbContext.Staffs.FindAsync([request.StaffId], ct);
        if (staff is null)
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự.");
        }

        var room = await _dbContext.Rooms.FindAsync([request.RoomId], ct);
        if (room is null)
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(404, "Không tìm thấy", "Không tìm thấy phòng.");
        }

        if (!Enum.TryParse<ShiftName>(request.ShiftName, ignoreCase: true, out var shiftName))
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(400, "Ca trực không hợp lệ", "Ca trực hợp lệ: Morning, Afternoon, FullDay.");
        }

        if (request.EndsAtUtc <= request.StartsAtUtc)
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(400, "Thời gian không hợp lệ", "Thời gian kết thúc ca trực phải sau thời gian bắt đầu.");
        }

        var assignment = new StaffShiftAssignment
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            RoomId = request.RoomId,
            ShiftDate = request.ShiftDate,
            ShiftName = shiftName,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            IsShiftLeader = request.IsShiftLeader
        };

        _dbContext.StaffShiftAssignments.Add(assignment);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "StaffId") || IsUniqueViolation(ex, "ShiftDate"))
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    409,
                    "Trùng ca trực",
                    $"Nhân viên '{staff.FullName}' đã được phân công ca trực trong ngày {request.ShiftDate:yyyy-MM-dd}.");
            }

            if (IsUniqueViolation(ex, "IsShiftLeader") || IsUniqueViolation(ex, "RoomId"))
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    409,
                    "Trùng trưởng ca",
                    $"Phòng '{room.RoomNumber}' đã có trưởng ca được phân công cho ca {request.ShiftName} ngày {request.ShiftDate:yyyy-MM-dd}.");
            }

            return ServiceResult<ShiftAssignmentDto>.Failure(409, "Xung đột dữ liệu", "Không thể phân ca trực do xung đột dữ liệu.");
        }

        var dto = new ShiftAssignmentDto(
            assignment.Id,
            staff.Id,
            staff.FullName,
            staff.StaffCode,
            room.Id,
            room.RoomNumber,
            room.RoomName,
            assignment.ShiftDate,
            assignment.ShiftName.ToString(),
            assignment.StartsAtUtc,
            assignment.EndsAtUtc,
            assignment.IsShiftLeader);

        return ServiceResult<ShiftAssignmentDto>.Success(dto, 201);
    }

    public async Task<ServiceResult> DeleteShiftAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await _dbContext.StaffShiftAssignments.FindAsync([id], ct);
        if (assignment is null)
        {
            return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy ca trực.");
        }

        _dbContext.StaffShiftAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(ct);

        return ServiceResult.Success(204);
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string keyPart)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            return string.IsNullOrEmpty(keyPart) || sqlEx.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
    }
}
