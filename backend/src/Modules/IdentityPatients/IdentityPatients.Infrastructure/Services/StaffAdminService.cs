using System.Security.Claims;
using System.Text.Json;
using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class StaffAdminService(
    IdentityPatientsDbContext dbContext,
    IUnitOfWork unitOfWork,
    AuditLogWriter auditLog,
    IHttpContextAccessor? httpContextAccessor = null) : IStaffAdminService
{
    public const string AdminManagedByAdminCode = "AdminManagedByAdmin";
    public const string LastActiveAdminCode = "LastActiveAdmin";
    public const string ShiftAlreadyAssignedCode = "ShiftAlreadyAssigned";
    public const string ShiftLeaderExistsCode = "ShiftLeaderExists";
    public const string SpecialtyHasActiveDoctorsCode = "SpecialtyHasActiveDoctors";
    public const string InactiveStaffCode = "InactiveStaff";
    public const string InactiveRoomCode = "InactiveRoom";

    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    private Guid? ResolveActorUserId(Guid? explicitActor = null)
    {
        if (explicitActor.HasValue && explicitActor.Value != Guid.Empty)
        {
            return explicitActor.Value;
        }

        var user = httpContextAccessor?.HttpContext?.User;
        var sub = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private Task WriteAuditAsync(
        string tableName,
        Guid recordId,
        ActionType actionType,
        string changedColumns,
        object? oldValues,
        object? newValues,
        string reason,
        CancellationToken ct,
        Guid? explicitActor = null,
        Guid? shiftAssignmentId = null) =>
        auditLog.WriteAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            PerformedByUserId = ResolveActorUserId(explicitActor),
            StaffShiftAssignmentId = shiftAssignmentId,
            TableName = tableName,
            RecordId = recordId,
            ActionType = actionType,
            ChangedColumns = changedColumns,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            ChangeReason = reason,
            TimestampUtc = DateTime.UtcNow
        }, ct);

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

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.Specialties.Add(specialty);
                await _dbContext.SaveChangesAsync(token);
                await WriteAuditAsync(
                    "identity.Specialty",
                    specialty.Id,
                    ActionType.INSERT,
                    "SpecialtyCode,SpecialtyName,Description,IsActive",
                    null,
                    new { specialty.SpecialtyCode, specialty.SpecialtyName, specialty.Description, specialty.IsActive },
                    $"Admin tạo chuyên khoa {specialty.SpecialtyCode}",
                    token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<SpecialtyAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên chuyên khoa không được để trống.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var specialty = await _dbContext.Specialties.FindAsync([id], token);
            if (specialty is null)
            {
                return ServiceResult<SpecialtyAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy chuyên khoa.");
            }

            if (specialty.IsActive && !request.IsActive)
            {
                var hasActiveDoctors = await _dbContext.Doctors.AsNoTracking()
                    .AnyAsync(d => d.SpecialtyId == id && d.IsActive, token);
                if (hasActiveDoctors)
                {
                    return ServiceResult<SpecialtyAdminDto>.Failure(
                        409,
                        "Không thể tắt chuyên khoa",
                        "Chuyên khoa vẫn còn bác sĩ đang hoạt động. Vui lòng chuyển chuyên khoa hoặc vô hiệu hóa các bác sĩ liên quan trước.",
                        code: SpecialtyHasActiveDoctorsCode);
                }
            }

            var oldValues = new { specialty.SpecialtyName, specialty.Description, specialty.IsActive };

            // Quy tắc: SpecialtyCode KHÔNG cho đổi sau khi tạo (chatbot tham chiếu)
            specialty.SpecialtyName = request.Name.Trim();
            specialty.Description = request.Description?.Trim();
            specialty.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(token);
            await WriteAuditAsync(
                "identity.Specialty",
                specialty.Id,
                ActionType.UPDATE,
                "SpecialtyName,Description,IsActive",
                oldValues,
                new { specialty.SpecialtyName, specialty.Description, specialty.IsActive },
                $"Admin cập nhật chuyên khoa {specialty.SpecialtyCode}",
                token);

            return ServiceResult<SpecialtyAdminDto>.Success(
                new SpecialtyAdminDto(specialty.Id, specialty.SpecialtyCode, specialty.SpecialtyName, specialty.Description, specialty.IsActive));
        }, ct);
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
        if (!Enum.TryParse<RankCode>(request.Code, ignoreCase: true, out var rankCode) || !Enum.IsDefined(rankCode))
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

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.DoctorRanks.Add(rank);
                await _dbContext.SaveChangesAsync(token);
                await WriteAuditAsync(
                    "identity.DoctorRank",
                    rank.Id,
                    ActionType.INSERT,
                    "RankCode,RankName,DisplayOrder,IsActive",
                    null,
                    new { RankCode = rank.RankCode.ToString(), rank.RankName, rank.DisplayOrder, rank.IsActive },
                    $"Admin tạo cấp bậc bác sĩ {rank.RankCode}",
                    token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<DoctorRankAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên cấp bậc không được để trống.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var rank = await _dbContext.DoctorRanks.FindAsync([id], token);
            if (rank is null)
            {
                return ServiceResult<DoctorRankAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy cấp bậc.");
            }

            var oldValues = new { rank.RankName, rank.DisplayOrder, rank.IsActive };

            // RankCode cố định sau khi tạo vì Billing tham chiếu theo mã cấp bậc
            rank.RankName = request.Name.Trim();
            rank.DisplayOrder = request.DisplayOrder;
            rank.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(token);
            await WriteAuditAsync(
                "identity.DoctorRank",
                rank.Id,
                ActionType.UPDATE,
                "RankName,DisplayOrder,IsActive",
                oldValues,
                new { rank.RankName, rank.DisplayOrder, rank.IsActive },
                $"Admin cập nhật cấp bậc bác sĩ {rank.RankCode}",
                token);

            return ServiceResult<DoctorRankAdminDto>.Success(
                new DoctorRankAdminDto(rank.Id, rank.RankCode.ToString(), rank.RankName, rank.DisplayOrder, rank.IsActive));
        }, ct);
    }

    // --- Rooms ---

    public async Task<ServiceResult<IReadOnlyList<RoomAdminDto>>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _dbContext.Rooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(roomType) && Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var parsedType) && Enum.IsDefined(parsedType))
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

        if (!Enum.TryParse<RoomType>(request.RoomType, ignoreCase: true, out var roomType) || !Enum.IsDefined(roomType))
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

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.Rooms.Add(room);
                await _dbContext.SaveChangesAsync(token);
                await WriteAuditAsync(
                    "identity.Room",
                    room.Id,
                    ActionType.INSERT,
                    "RoomNumber,RoomName,RoomType,DepartmentName,IsActive",
                    null,
                    new { room.RoomNumber, room.RoomName, RoomType = room.RoomType.ToString(), room.DepartmentName, room.IsActive },
                    $"Admin tạo phòng {room.RoomNumber}",
                    token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
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
        if (string.IsNullOrWhiteSpace(request.RoomName))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên phòng không được để trống.");
        }

        if (!Enum.TryParse<RoomType>(request.RoomType, ignoreCase: true, out var roomType) || !Enum.IsDefined(roomType))
        {
            return ServiceResult<RoomAdminDto>.Failure(400, "Loại phòng không hợp lệ", "Loại phòng hợp lệ: Clinical, Laboratory, XRay, Ultrasound, Endoscopy.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var room = await _dbContext.Rooms.FindAsync([id], token);
            if (room is null)
            {
                return ServiceResult<RoomAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy phòng.");
            }

            var oldValues = new { room.RoomName, RoomType = room.RoomType.ToString(), room.DepartmentName, room.IsActive };

            room.RoomName = request.RoomName.Trim();
            room.RoomType = roomType;
            room.DepartmentName = request.DepartmentName.Trim();
            room.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(token);
            await WriteAuditAsync(
                "identity.Room",
                room.Id,
                ActionType.UPDATE,
                "RoomName,RoomType,DepartmentName,IsActive",
                oldValues,
                new { room.RoomName, RoomType = room.RoomType.ToString(), room.DepartmentName, room.IsActive },
                $"Admin cập nhật phòng {room.RoomNumber}",
                token);

            return ServiceResult<RoomAdminDto>.Success(
                new RoomAdminDto(room.Id, room.RoomNumber, room.RoomName, room.RoomType.ToString(), room.DepartmentName, room.IsActive));
        }, ct);
    }

    // --- Staff ---

    public async Task<ServiceResult<IReadOnlyList<StaffAdminDto>>> GetStaffListAsync(
        string? staffType = null,
        bool? isActive = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        var query = from s in _dbContext.Staffs.AsNoTracking()
                    join u in _dbContext.AppUsers.AsNoTracking() on s.AppUserId equals u.Id
                    select new { Staff = s, User = u };

        if (!string.IsNullOrWhiteSpace(staffType) && Enum.TryParse<StaffType>(staffType, ignoreCase: true, out var parsedType) && Enum.IsDefined(parsedType))
        {
            query = query.Where(x => x.Staff.StaffType == parsedType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.Staff.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Staff.FullName.Contains(term) ||
                x.Staff.StaffCode.Contains(term) ||
                x.User.UserName.Contains(term) ||
                (x.User.Email != null && x.User.Email.Contains(term)) ||
                (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(term)));
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

    public async Task<ServiceResult<StaffAdminDto>> CreateStaffAsync(Guid actorUserId, CreateStaffRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Tên đăng nhập và mật khẩu không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.StaffCode) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Mã nhân viên và họ tên không được để trống.");
        }

        if (request.Password.Length < PasswordHashing.MinPasswordLength)
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", $"Mật khẩu phải có ít nhất {PasswordHashing.MinPasswordLength} ký tự.");
        }

        // Technician chưa có vai trò đăng nhập (D3), nên không tạo tài khoản cho loại này.
        if (!Enum.TryParse<StaffType>(request.StaffType, ignoreCase: true, out var staffType) || !Enum.IsDefined(staffType) || staffType == StaffType.Technician)
        {
            return ServiceResult<StaffAdminDto>.Failure(400, "Loại nhân sự không hợp lệ", "Loại nhân sự hợp lệ: Doctor, Nurse, Receptionist, Admin.");
        }

        if (staffType == StaffType.Admin && !await IsAdminAsync(actorUserId, ct))
        {
            return AdminOnlyFailure<StaffAdminDto>();
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            PasswordHash = PasswordHashing.Hash(request.Password),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EmailConfirmed = true,
            PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(request.PhoneNumber),
            IsActive = true,
            CreatedAtUtc = now
        };
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = user.Id,
            StaffCode = request.StaffCode.Trim().ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            StaffType = staffType,
            IsActive = true,
            CreatedAtUtc = now
        };

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.AppUsers.Add(user);
                _dbContext.Staffs.Add(staff);
                await _dbContext.SaveChangesAsync(token);
                await WriteAuditAsync(
                    "identity.Staff",
                    staff.Id,
                    ActionType.INSERT,
                    "StaffCode,FullName,StaffType,AppUserId,IsActive",
                    null,
                    new { staff.StaffCode, staff.FullName, StaffType = staff.StaffType.ToString(), user.UserName, staff.IsActive },
                    $"Admin tạo nhân sự {staff.StaffCode}",
                    token,
                    actorUserId);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "UserName"))
            {
                return ServiceResult<StaffAdminDto>.Failure(409, "Trùng tên đăng nhập", "Tên đăng nhập đã tồn tại.", code: "UserNameTaken");
            }
            if (IsUniqueViolation(ex, "Email") || IsUniqueViolation(ex, "PhoneNumber"))
            {
                return ServiceResult<StaffAdminDto>.Failure(409, "Trùng liên hệ", "Email hoặc số điện thoại đã được dùng cho tài khoản khác.", code: "ContactTaken");
            }
            if (IsUniqueViolation(ex, "StaffCode"))
            {
                return ServiceResult<StaffAdminDto>.Failure(409, "Trùng mã nhân viên", "Mã nhân viên đã tồn tại.", code: "StaffCodeTaken");
            }
            return ServiceResult<StaffAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo nhân sự do xung đột dữ liệu.");
        }

        return ServiceResult<StaffAdminDto>.Success(ToDto(staff, user), 201);
    }

    public Task<ServiceResult<StaffAdminDto>> UpdateStaffAsync(Guid actorUserId, Guid id, UpdateStaffRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Task.FromResult(ServiceResult<StaffAdminDto>.Failure(400, "Dữ liệu không hợp lệ", "Họ tên không được để trống."));
        }

        return ChangeStaffAsync(actorUserId, id, request.IsActive, staff => staff.FullName = request.FullName.Trim(), "FullName,IsActive", ct);
    }

    public Task<ServiceResult<StaffAdminDto>> ToggleStaffActiveAsync(Guid actorUserId, Guid id, bool isActive, CancellationToken ct = default) =>
        ChangeStaffAsync(actorUserId, id, isActive, _ => { }, "IsActive", ct);

    // Staff, AppUser và Doctor đổi IsActive trong cùng một unit of work.
    // Nhân sự loại Admin chỉ Admin sửa được; không vô hiệu hóa Admin đang hoạt động cuối cùng (D5).
    private async Task<ServiceResult<StaffAdminDto>> ChangeStaffAsync(
        Guid actorUserId,
        Guid id,
        bool isActive,
        Action<Staff> apply,
        string changedColumns,
        CancellationToken ct)
    {
        var actorIsAdmin = await IsAdminAsync(actorUserId, ct);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var staff = await _dbContext.Staffs.FindAsync([id], token);
            if (staff is null)
            {
                return ServiceResult<StaffAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự.");
            }

            if (staff.StaffType == StaffType.Admin)
            {
                if (!actorIsAdmin)
                {
                    return AdminOnlyFailure<StaffAdminDto>();
                }

                if (staff.IsActive && !isActive)
                {
                    // Khóa mọi Admin đang hoạt động để hai lệnh vô hiệu hóa đồng thời không cùng thấy "còn Admin khác".
                    var activeAdmins = await _dbContext.Staffs
                        .FromSql($"SELECT * FROM [identity].[Staff] WITH (UPDLOCK, HOLDLOCK) WHERE [StaffType] = N'Admin' AND [IsActive] = 1")
                        .AsNoTracking()
                        .Select(s => s.Id)
                        .ToListAsync(token);
                    if (activeAdmins.All(adminId => adminId == staff.Id))
                    {
                        return ServiceResult<StaffAdminDto>.Failure(409, "Không thể vô hiệu hóa", "Đây là tài khoản quản trị đang hoạt động cuối cùng.", code: LastActiveAdminCode);
                    }
                }
            }

            var oldValues = new { staff.FullName, staff.IsActive };
            apply(staff);
            staff.IsActive = isActive;
            var user = await _dbContext.AppUsers.FindAsync([staff.AppUserId], token);
            if (user != null)
            {
                user.IsActive = isActive;
            }

            if (staff.StaffType == StaffType.Doctor)
            {
                var doctor = await _dbContext.Doctors.FirstOrDefaultAsync(d => d.StaffId == staff.Id, token);
                if (doctor != null)
                {
                    doctor.IsActive = isActive;
                }
            }

            await _dbContext.SaveChangesAsync(token);
            await WriteAuditAsync(
                "identity.Staff",
                staff.Id,
                ActionType.UPDATE,
                changedColumns,
                oldValues,
                new { staff.FullName, staff.IsActive },
                $"Admin cập nhật nhân sự {staff.StaffCode}",
                token,
                actorUserId);

            return ServiceResult<StaffAdminDto>.Success(ToDto(staff, user));
        }, ct);
    }

    private async Task<bool> IsAdminAsync(Guid appUserId, CancellationToken ct) =>
        (await AccountRoles.ResolveAsync(_dbContext, appUserId, ct))?.Role == Roles.Admin;

    public async Task<ServiceResult> ResetStaffPasswordAsync(Guid actorUserId, Guid staffId, ResetStaffPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < PasswordHashing.MinPasswordLength)
        {
            return ServiceResult.Failure(400, "Dữ liệu không hợp lệ", $"Mật khẩu phải có ít nhất {PasswordHashing.MinPasswordLength} ký tự.");
        }

        return await unitOfWork.ExecuteAsync(async token =>
        {
            var staff = await _dbContext.Staffs.FindAsync([staffId], token);
            if (staff is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy nhân viên.");
            }

            if (staff.StaffType == StaffType.Admin && !await IsAdminAsync(actorUserId, token))
            {
                return ServiceResult.Failure(403, "Không có quyền", "Chỉ quản trị viên được quản lý nhân sự loại Admin.", code: AdminManagedByAdminCode);
            }

            var user = await _dbContext.AppUsers.FindAsync([staff.AppUserId], token);
            if (user is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy tài khoản người dùng của nhân viên.");
            }

            user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
            await _dbContext.SaveChangesAsync(token);

            await WriteAuditAsync(
                "identity.AppUser",
                user.Id,
                ActionType.UPDATE,
                "PasswordHash",
                null,
                null,
                $"Admin đặt lại mật khẩu cho nhân viên {staff.StaffCode}",
                token,
                actorUserId);

            return ServiceResult.Success();
        }, ct);
    }

    private static ServiceResult<T> AdminOnlyFailure<T>() =>
        ServiceResult<T>.Failure(403, "Không có quyền", "Chỉ quản trị viên được quản lý nhân sự loại Admin.", code: AdminManagedByAdminCode);

    private static StaffAdminDto ToDto(Staff staff, AppUser? user) => new(
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
            Biography = request.Biography?.Trim(),
            IsActive = true
        };

        try
        {
            await unitOfWork.ExecuteAsync(async token =>
            {
                _dbContext.Doctors.Add(doctor);
                await _dbContext.SaveChangesAsync(token);
                await WriteAuditAsync(
                    "identity.Doctor",
                    doctor.Id,
                    ActionType.INSERT,
                    "StaffId,SpecialtyId,DoctorRankId,Biography,IsActive",
                    null,
                    new { doctor.StaffId, doctor.SpecialtyId, doctor.DoctorRankId, doctor.IsActive },
                    $"Admin tạo hồ sơ bác sĩ cho nhân viên {staff.StaffCode}",
                    token);
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "StaffId"))
            {
                return ServiceResult<DoctorAdminDto>.Failure(409, "Bác sĩ đã tồn tại", "Nhân sự này đã có thông tin bác sĩ trong hệ thống.");
            }
            return ServiceResult<DoctorAdminDto>.Failure(409, "Xung đột dữ liệu", "Không thể tạo bác sĩ do xung đột dữ liệu.");
        }

        return ServiceResult<DoctorAdminDto>.Success(new DoctorAdminDto(
            doctor.Id,
            doctor.StaffId,
            staff.FullName,
            staff.StaffCode,
            specialty.Id,
            specialty.SpecialtyName,
            rank.Id,
            rank.RankName,
            doctor.Biography,
            doctor.IsActive), 201);
    }

    public async Task<ServiceResult<DoctorAdminDto>> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var doctor = await _dbContext.Doctors.FindAsync([id], token);
            if (doctor is null)
            {
                return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy bác sĩ.");
            }

            var specialty = await _dbContext.Specialties.FindAsync([request.SpecialtyId], token);
            if (specialty is null)
            {
                return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy chuyên khoa.");
            }

            var rank = await _dbContext.DoctorRanks.FindAsync([request.DoctorRankId], token);
            if (rank is null)
            {
                return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy cấp bậc bác sĩ.");
            }

            var oldValues = new { doctor.SpecialtyId, doctor.DoctorRankId, doctor.Biography, doctor.IsActive };

            doctor.SpecialtyId = request.SpecialtyId;
            doctor.DoctorRankId = request.DoctorRankId;
            doctor.Biography = request.Biography?.Trim();
            doctor.IsActive = request.IsActive;

            var staff = await _dbContext.Staffs.FindAsync([doctor.StaffId], token);

            await _dbContext.SaveChangesAsync(token);
            await WriteAuditAsync(
                "identity.Doctor",
                doctor.Id,
                ActionType.UPDATE,
                "SpecialtyId,DoctorRankId,Biography,IsActive",
                oldValues,
                new { doctor.SpecialtyId, doctor.DoctorRankId, doctor.Biography, doctor.IsActive },
                $"Admin cập nhật thông tin bác sĩ {staff?.StaffCode ?? doctor.Id.ToString()}",
                token);

            return ServiceResult<DoctorAdminDto>.Success(new DoctorAdminDto(
                doctor.Id,
                doctor.StaffId,
                staff?.FullName ?? string.Empty,
                staff?.StaffCode ?? string.Empty,
                specialty.Id,
                specialty.SpecialtyName,
                rank.Id,
                rank.RankName,
                doctor.Biography,
                doctor.IsActive));
        }, ct);
    }

    public async Task<ServiceResult<DoctorAdminDto>> ToggleDoctorActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var doctor = await _dbContext.Doctors.FindAsync([id], token);
            if (doctor is null)
            {
                return ServiceResult<DoctorAdminDto>.Failure(404, "Không tìm thấy", "Không tìm thấy bác sĩ.");
            }

            var oldActive = doctor.IsActive;
            doctor.IsActive = isActive;

            var staff = await _dbContext.Staffs.FindAsync([doctor.StaffId], token);
            var specialty = await _dbContext.Specialties.FindAsync([doctor.SpecialtyId], token);
            var rank = await _dbContext.DoctorRanks.FindAsync([doctor.DoctorRankId], token);

            await _dbContext.SaveChangesAsync(token);
            if (oldActive != isActive)
            {
                await WriteAuditAsync(
                    "identity.Doctor",
                    doctor.Id,
                    ActionType.UPDATE,
                    "IsActive",
                    new { IsActive = oldActive },
                    new { IsActive = isActive },
                    $"Admin {(isActive ? "kích hoạt" : "vô hiệu hóa")} bác sĩ {staff?.StaffCode ?? doctor.Id.ToString()}",
                    token);
            }

            return ServiceResult<DoctorAdminDto>.Success(new DoctorAdminDto(
                doctor.Id,
                doctor.StaffId,
                staff?.FullName ?? string.Empty,
                staff?.StaffCode ?? string.Empty,
                specialty?.Id ?? Guid.Empty,
                specialty?.SpecialtyName ?? string.Empty,
                rank?.Id ?? Guid.Empty,
                rank?.RankName ?? string.Empty,
                doctor.Biography,
                doctor.IsActive));
        }, ct);
    }

    // --- Shift Assignments ---

    public async Task<ServiceResult<IReadOnlyList<ShiftAssignmentDto>>> GetShiftAssignmentsAsync(
        DateOnly? date = null,
        Guid? roomId = null,
        Guid? staffId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
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
        else
        {
            if (fromDate.HasValue)
            {
                query = query.Where(x => x.Assignment.ShiftDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.Assignment.ShiftDate <= toDate.Value);
            }
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
        if (!Enum.TryParse<ShiftName>(request.ShiftName, ignoreCase: true, out var shiftName) || !Enum.IsDefined(shiftName))
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(400, "Ca trực không hợp lệ", "Ca trực hợp lệ: Morning, Afternoon, FullDay.");
        }

        if (request.EndsAtUtc <= request.StartsAtUtc)
        {
            return ServiceResult<ShiftAssignmentDto>.Failure(400, "Thời gian không hợp lệ", "Thời gian kết thúc ca trực phải sau thời gian bắt đầu.");
        }

        // ERD không có unique cho hai quy tắc dưới (Backend kiểm). Khóa hàng Staff (một ca mỗi ngày / không trùng giờ) và hàng Room
        // (một trưởng ca mỗi ca) để hai yêu cầu đồng thời không cùng vượt qua bước kiểm tra.
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var staff = await _dbContext.Staffs
                .FromSql($"SELECT * FROM [identity].[Staff] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.StaffId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(token);
            if (staff is null)
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(404, "Không tìm thấy", "Không tìm thấy nhân sự.");
            }

            if (!staff.IsActive)
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    400,
                    "Nhân sự không hoạt động",
                    $"Nhân viên '{staff.FullName}' đang bị vô hiệu hóa, không thể gán ca trực.",
                    code: InactiveStaffCode);
            }

            var room = await _dbContext.Rooms
                .FromSql($"SELECT * FROM [identity].[Room] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.RoomId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(token);
            if (room is null)
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(404, "Không tìm thấy", "Không tìm thấy phòng.");
            }

            if (!room.IsActive)
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    400,
                    "Phòng không hoạt động",
                    $"Phòng '{room.RoomNumber}' đang tạm ngưng hoạt động, không thể gán ca trực.",
                    code: InactiveRoomCode);
            }

            var hasOverlap = await _dbContext.StaffShiftAssignments.AnyAsync(
                a => a.StaffId == staff.Id
                     && (a.ShiftDate == request.ShiftDate
                         || (a.StartsAtUtc < request.EndsAtUtc && a.EndsAtUtc > request.StartsAtUtc)),
                token);

            if (hasOverlap)
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    409,
                    "Trùng ca trực",
                    $"Nhân viên '{staff.FullName}' đã được phân công ca trực trong ngày {request.ShiftDate:yyyy-MM-dd} hoặc trùng khung giờ.",
                    code: ShiftAlreadyAssignedCode);
            }

            if (request.IsShiftLeader && await _dbContext.StaffShiftAssignments.AnyAsync(
                    a => a.RoomId == room.Id && a.ShiftDate == request.ShiftDate && a.ShiftName == shiftName && a.IsShiftLeader, token))
            {
                return ServiceResult<ShiftAssignmentDto>.Failure(
                    409,
                    "Trùng trưởng ca",
                    $"Phòng '{room.RoomNumber}' đã có trưởng ca cho ca {shiftName} ngày {request.ShiftDate:yyyy-MM-dd}.",
                    code: ShiftLeaderExistsCode);
            }

            var assignment = new StaffShiftAssignment
            {
                Id = Guid.NewGuid(),
                StaffId = staff.Id,
                RoomId = room.Id,
                ShiftDate = request.ShiftDate,
                ShiftName = shiftName,
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc,
                IsShiftLeader = request.IsShiftLeader
            };
            _dbContext.StaffShiftAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync(token);

            await WriteAuditAsync(
                "identity.StaffShiftAssignment",
                assignment.Id,
                ActionType.INSERT,
                "StaffId,RoomId,ShiftDate,ShiftName,StartsAtUtc,EndsAtUtc,IsShiftLeader",
                null,
                new { staff.StaffCode, room.RoomNumber, assignment.ShiftDate, ShiftName = assignment.ShiftName.ToString(), assignment.IsShiftLeader },
                $"Admin phân công ca trực cho {staff.StaffCode} tại phòng {room.RoomNumber}",
                token,
                shiftAssignmentId: assignment.Id);

            return ServiceResult<ShiftAssignmentDto>.Success(new ShiftAssignmentDto(
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
                assignment.IsShiftLeader), 201);
        }, ct);
    }

    public async Task<ServiceResult> DeleteShiftAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var assignment = await _dbContext.StaffShiftAssignments.FindAsync([id], token);
            if (assignment is null)
            {
                return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy ca trực.");
            }

            var oldValues = new
            {
                assignment.StaffId,
                assignment.RoomId,
                assignment.ShiftDate,
                ShiftName = assignment.ShiftName.ToString(),
                assignment.IsShiftLeader
            };

            _dbContext.StaffShiftAssignments.Remove(assignment);
            await _dbContext.SaveChangesAsync(token);

            await WriteAuditAsync(
                "identity.StaffShiftAssignment",
                assignment.Id,
                ActionType.DELETE,
                "StaffId,RoomId,ShiftDate,ShiftName",
                oldValues,
                null,
                $"Admin xóa phân công ca trực ngày {assignment.ShiftDate:yyyy-MM-dd}",
                token,
                shiftAssignmentId: assignment.Id);

            return ServiceResult.Success(204);
        }, ct);
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
