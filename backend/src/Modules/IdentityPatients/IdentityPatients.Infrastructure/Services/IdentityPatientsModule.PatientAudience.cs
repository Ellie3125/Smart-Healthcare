using IdentityPatients.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public async Task<IReadOnlyList<Guid>> GetPatientAppUserIdsAsync(Guid patientId, CancellationToken ct = default)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("PatientId is required.", nameof(patientId));
        return await _dbContext.PatientRepresentatives.AsNoTracking()
            .Where(r => r.PatientId == patientId && r.IsActive
                && (r.VerificationStatus == VerificationStatus.Pending || r.VerificationStatus == VerificationStatus.Verified)
                && _dbContext.AppUsers.Any(u => u.Id == r.AppUserId && u.IsActive)
                && !_dbContext.RoomAccounts.Any(a => a.AppUserId == r.AppUserId)
                && !_dbContext.Staffs.Any(s => s.AppUserId == r.AppUserId))
            .Select(r => r.AppUserId).Distinct().ToListAsync(ct);
    }
}
