using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public async Task<PatientJourneyProfile?> GetJourneyPatientAsync(Guid appUserId, Guid? patientId, CancellationToken ct = default)
    {
        if (appUserId == Guid.Empty || patientId == Guid.Empty) return null;
        if (patientId is null)
        {
            var own = await _dbContext.PatientRepresentatives.AsNoTracking()
                .Where(r => r.AppUserId == appUserId && r.IsActive && r.Relationship == Relationship.Self && r.VerificationStatus == VerificationStatus.Verified)
                .Select(r => r.PatientId).Distinct().Take(2).ToListAsync(ct);
            if (own.Count != 1) return null;
            patientId = own[0];
        }
        if (!await CanAccessPatientAsync(appUserId, patientId.Value, AccessLevel.ClinicalRecords, ct)) return null;
        return await _dbContext.Patients.AsNoTracking().Where(p => p.Id == patientId.Value)
            .Select(p => new PatientJourneyProfile(p.Id, p.FullName)).SingleOrDefaultAsync(ct);
    }
}
