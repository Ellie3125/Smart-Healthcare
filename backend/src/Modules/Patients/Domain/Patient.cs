using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Domain;

/// <summary>Hồ sơ hành chính (bảng dbo.Patient). PatientId nội bộ khác CCCD; hồ sơ được phép chưa có CCCD (MED-03).</summary>
public sealed class Patient
{
    private Patient()
    {
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = "";
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? NationalId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Address { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Token khóa lạc quan; sửa hồ sơ phải gửi lại phiên bản đã đọc.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static Patient Create(PatientDetails details, DateTime nowUtc)
    {
        var patient = new Patient { Id = Guid.CreateVersion7(), CreatedAtUtc = nowUtc };
        patient.Apply(details);
        return patient;
    }

    /// <summary>CCCD chỉ được bổ sung khi còn trống; đổi CCCD đã có cần nhân viên xác minh nên bị từ chối ở đây.</summary>
    public Result Update(PatientDetails details)
    {
        if (NationalId is not null && details.NationalId is not null && details.NationalId != NationalId)
            return Error.Conflict("national_id_locked", "CCCD đã ghi nhận không tự sửa được; liên hệ quầy tiếp đón.");

        Apply(details with { NationalId = details.NationalId ?? NationalId });
        return Result.Success();
    }

    private void Apply(PatientDetails details)
    {
        FullName = details.FullName;
        DateOfBirth = details.DateOfBirth;
        Gender = details.Gender;
        NationalId = details.NationalId;
        PhoneNumber = details.PhoneNumber;
        Address = details.Address;
    }
}
