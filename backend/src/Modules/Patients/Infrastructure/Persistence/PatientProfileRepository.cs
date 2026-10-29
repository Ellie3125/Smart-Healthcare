using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Patients.Application;
using SmartHealthcare.Patients.Domain;

namespace SmartHealthcare.Patients.Infrastructure.Persistence;

internal sealed class PatientProfileRepository(PatientsModuleDbContext db) : IPatientProfileRepository
{
    public Task<bool> HasActiveSelfLinkAsync(Guid appUserId, CancellationToken ct) =>
        db.Representatives.AnyAsync(l => l.AppUserId == appUserId && l.IsActive && l.Relationship == Relationship.Self, ct);

    public async Task<AddProfileOutcome> TryAddAsync(Patient patient, PatientRepresentative link, CancellationToken ct)
    {
        db.Patients.Add(patient);
        db.Representatives.Add(link);
        try
        {
            await db.SaveChangesAsync(ct);
            return AddProfileOutcome.Added;
        }
        catch (DbUpdateException ex) when (UniqueIndexOf(ex) is { } index)
        {
            db.Entry(link).State = EntityState.Detached;
            db.Entry(patient).State = EntityState.Detached;
            return index == PatientsModuleDbContext.SelfLinkIndex ? AddProfileOutcome.SelfProfileExists : AddProfileOutcome.DuplicateNationalId;
        }
    }

    public async Task<IReadOnlyList<AccessibleProfile>> ListAccessibleAsync(Guid appUserId, CancellationToken ct)
    {
        var rows = await AccessibleTo(appUserId, patientId: null).AsNoTracking().ToListAsync(ct);
        return rows.Select(x => new AccessibleProfile(x.Patient, x.Link)).ToList();
    }

    public async Task<AccessibleProfile?> FindAccessibleAsync(Guid appUserId, Guid patientId, CancellationToken ct)
    {
        var row = await AccessibleTo(appUserId, patientId).SingleOrDefaultAsync(ct);
        return row is null ? null : new AccessibleProfile(row.Patient, row.Link);
    }

    public async Task<SaveProfileOutcome> SaveProfileAsync(Patient patient, byte[] expectedVersion, CancellationToken ct)
    {
        var entry = db.Entry(patient);
        // Không có cột nào đổi thì EF không phát lệnh UPDATE, nên tự so phiên bản để client cũ vẫn nhận xung đột.
        if (!entry.Properties.Any(p => p.IsModified))
            return expectedVersion.SequenceEqual(patient.RowVersion) ? SaveProfileOutcome.Saved : SaveProfileOutcome.VersionConflict;

        entry.Property(p => p.RowVersion).OriginalValue = expectedVersion;
        try
        {
            await db.SaveChangesAsync(ct);
            return SaveProfileOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveProfileOutcome.VersionConflict;
        }
        catch (DbUpdateException ex) when (UniqueIndexOf(ex) == PatientsModuleDbContext.NationalIdIndex)
        {
            return SaveProfileOutcome.DuplicateNationalId;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>Lọc và sắp xếp trước khi chiếu sang ProfileRow, vì EF không dịch được điều kiện đặt trên record đã chiếu.</summary>
    private IQueryable<ProfileRow> AccessibleTo(Guid appUserId, Guid? patientId) =>
        from link in db.Representatives
        join patient in db.Patients on link.PatientId equals patient.Id
        where link.AppUserId == appUserId && link.IsActive && link.VerificationStatus == VerificationStatus.Verified
              && (patientId == null || patient.Id == patientId)
        orderby link.Relationship == Relationship.Self descending, link.CreatedAtUtc
        select new ProfileRow(patient, link);

    /// <summary>Tên unique index bị vi phạm (SQL Server lỗi 2601/2627), hoặc null nếu không phải lỗi trùng.</summary>
    private static string? UniqueIndexOf(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException { Number: 2601 or 2627 } sql) return null;
        return sql.Message.Contains(PatientsModuleDbContext.SelfLinkIndex, StringComparison.Ordinal)
            ? PatientsModuleDbContext.SelfLinkIndex
            : sql.Message.Contains(PatientsModuleDbContext.NationalIdIndex, StringComparison.Ordinal)
                ? PatientsModuleDbContext.NationalIdIndex
                : "other";
    }

    private sealed record ProfileRow(Patient Patient, PatientRepresentative Link);
}
