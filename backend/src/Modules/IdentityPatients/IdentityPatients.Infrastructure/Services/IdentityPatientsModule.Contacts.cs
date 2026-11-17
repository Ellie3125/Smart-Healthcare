using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public async Task<IReadOnlyList<PatientContactDto>> GetPatientContactsAsync(IReadOnlyCollection<Guid> patientIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(patientIds);
        if (patientIds.Count > 100 || patientIds.Contains(Guid.Empty)) throw new ArgumentException("Contact lookup accepts at most one page of valid patient IDs.", nameof(patientIds));
        if (patientIds.Count == 0) return [];
        var ids = patientIds.Distinct().ToArray();
        var rows = await _dbContext.Patients.AsNoTracking().Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.FullName, p.PhoneNumber }).ToListAsync(ct);
        return rows.Select(p => new PatientContactDto(p.Id, p.FullName, Mask(p.PhoneNumber)!)).ToArray();
    }
}
