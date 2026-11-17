using Shared.Abstractions;

namespace Scheduling.Contracts;

/// <summary>A scheduled appointment keeps its session and fee when an admin assigns a substitute before the session starts.</summary>
public sealed record AppointmentDoctorChanged(Guid EventId, Guid AppointmentId, Guid PatientId, Guid SessionQuotaId,
    Guid OldDoctorId, Guid NewDoctorId, string NewDoctorName, DateOnly ServiceDate, string Reason,
    DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}
