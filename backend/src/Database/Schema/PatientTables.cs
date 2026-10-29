using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Hồ sơ bệnh nhân và quyền đại diện của tài khoản (sơ đồ 1).</summary>
internal static class PatientTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("Patient", t =>
        {
            t.Id();
            t.Req<string>("FullName", "nvarchar(150)");
            t.Req<DateOnly>("DateOfBirth", "date");
            t.Enum("Gender", 10, "Male", "Female", "Other");
            t.Opt<string>("NationalId", "varchar(12)");
            t.Opt<string>("PhoneNumber", "varchar(15)");
            t.Opt<string>("Address", "nvarchar(300)");
            t.UtcNow("CreatedAtUtc");
            // Không có trên sơ đồ; cần cho sửa hồ sơ (409 profile_version_conflict, Giải pháp kỹ thuật v2.1 mục 1.1).
            t.RowVersion();
            t.Rule("NationalId", "NationalId NOT LIKE '%[^0-9]%' AND LEN(NationalId) = 12");
            // UK trên sơ đồ; trẻ em có thể chưa có CCCD nên chỉ duy nhất khi có giá trị.
            t.UniqueWhere("UX_Patient_NationalId", "NationalId IS NOT NULL", "NationalId");
        });

        model.Table("PatientRepresentative", t =>
        {
            t.Id();
            t.Ref("AppUserId", "AppUser");
            t.Ref("PatientId", "Patient");
            t.Enum("Relationship", 10, "Self", "Child", "Parent", "Spouse", "Other");
            t.Opt<string>("AuthorizationProofDoc", "nvarchar(500)");
            t.Enum("VerificationStatus", 10, "Pending", "Verified", "Rejected", "Revoked").Default("N'Pending'");
            t.Flag("IsActive", true);
            t.UtcNow("CreatedAtUtc");
            t.OptUtc("RevokedAtUtc");
            // Mỗi tài khoản một liên kết đang hoạt động tới một hồ sơ, và tối đa một hồ sơ Self.
            t.UniqueWhere("UX_PatientRepresentative_ActiveLink", "IsActive = 1", "AppUserId", "PatientId");
            t.UniqueWhere("UX_PatientRepresentative_SelfPerAccount", "IsActive = 1 AND Relationship = N'Self'", "AppUserId");
        });
    }
}
