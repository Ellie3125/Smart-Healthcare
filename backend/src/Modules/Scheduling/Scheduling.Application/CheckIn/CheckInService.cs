using Scheduling.Contracts;
using Shared.Abstractions;
using Clinical.Contracts;
using IdentityPatients.Contracts;

namespace Scheduling.Application.CheckIn;

public sealed class CheckInService(ICheckInStore store, IUnitOfWork uow, IIntegrationEventPublisher publisher,
    IClinicalModule clinical, IIdentityPatientsModule identity)
{
    public async Task<CheckInResult> CheckInAsync(CheckInAppointment command, CancellationToken ct)
    {
        if (command is null || command.AppointmentId == Guid.Empty || command.PerformedByUserId == Guid.Empty
            || command.OperationId == Guid.Empty)
            throw new SchedulingException(SchedulingErrorCode.InvalidInput, "Định danh check-in không hợp lệ.");

        var outcome = await uow.ExecuteAsync(async t =>
        {
            var outcome = await store.CheckInAsync(command, async (appointment, token) =>
            {
                if (appointment.AlreadyCheckedIn)
                    return (await clinical.GetPatientEncountersAsync(appointment.PatientId, token))
                        .Single(x => x.AppointmentId == appointment.AppointmentId).Id;
                var doctor = await identity.GetDoctorAsync(appointment.DoctorId, token)
                    ?? throw new SchedulingException(SchedulingErrorCode.InvalidAppointmentState, "Không tìm thấy bác sĩ của phiên.");
                var specialty = (await identity.GetSpecialtiesAsync(token)).SingleOrDefault(s => s.Id == doctor.SpecialtyId);
                if (specialty is null || string.IsNullOrWhiteSpace(specialty.Name))
                    throw new SchedulingException(SchedulingErrorCode.InvalidAppointmentState, "Không tìm thấy tên chuyên khoa của bác sĩ.");
                try
                {
                    return (await clinical.StartEncounterAsync(new StartEncounterCommand(appointment.AppointmentId,
                        appointment.PatientId, appointment.DoctorId, appointment.AssignedRoomId, specialty.Name, doctor.RankName), token)).Id;
                }
                catch (ClinicalException e) when (e.Code == ClinicalErrorCode.ActiveEncounterAlreadyExists)
                {
                    throw new SchedulingException(SchedulingErrorCode.InvalidAppointmentState, "Bệnh nhân đang có lượt khám chưa kết thúc.");
                }
            }, t);
            if (outcome.Result is { AlreadyCheckedIn: false } result)
                await publisher.PublishAsync(new TicketCheckedIn(Guid.CreateVersion7(), result.AppointmentId,
                    result.PatientId, result.DoctorId, outcome.AssignedRoomId, result.ServiceDate, result.CheckedInAtUtc,
                    result.QueueTicketId, result.CheckedInAtUtc, command.OperationId, result.EncounterId), result.QueueTicketId, t);
            return outcome;
        }, ct);

        // Quá hạn có thể vừa ghi Absent; trả lỗi sau commit để không rollback trạng thái đó.
        if (outcome.Error is { } error)
            throw new SchedulingException(error, "Lịch hẹn không đủ điều kiện check-in.");
        var result = outcome.Result!;
        var roomNumber = (await identity.GetRoomsAsync(ct: ct)).SingleOrDefault(r => r.Id == outcome.AssignedRoomId)?.Code;
        return result with { DisplayNumber = TicketDisplayNumber.Format(roomNumber, "InitialConsultation", result.SequenceNumber) };
    }
}

public sealed record CheckInOutcome(CheckInResult? Result = null, Guid AssignedRoomId = default,
    SchedulingErrorCode? Error = null);

public sealed record TodayAppointment(Guid AppointmentId, string AppointmentNumber, Guid PatientId, Guid DoctorId,
    Guid AssignedRoomId, DateOnly ServiceDate, string Status, DateTime CheckInDeadlineUtc);

public sealed record AppointmentForCheckIn(Guid AppointmentId, Guid PatientId, Guid DoctorId, Guid AssignedRoomId,
    bool AlreadyCheckedIn);

public interface ICheckInStore
{
    /// <summary>Gọi getEncounter dưới khóa quota/appointment trong UoW: lấy Encounter cũ khi replay, mở mới khi hợp lệ.</summary>
    Task<CheckInOutcome> CheckInAsync(CheckInAppointment command,
        Func<AppointmentForCheckIn, CancellationToken, Task<Guid>> getEncounter, CancellationToken ct);
    Task<IReadOnlyList<TodayAppointment>> ListTodayAsync(Guid? doctorId, string? appointmentNumber, int skip, int take,
        CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindOverdueQuotasAsync(int take, CancellationToken ct);
    Task<int> MarkAbsentAsync(Guid quotaId, CancellationToken ct);
}

/// <summary>Host Worker đăng ký recurring mỗi phút; mỗi phiên là một transaction có cùng khóa với check-in.</summary>
public sealed class CheckInDeadlineJob(ICheckInStore store, IUnitOfWork uow)
{
    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var id in await store.FindOverdueQuotasAsync(100, ct))
            await uow.ExecuteAsync(t => store.MarkAbsentAsync(id, t), ct);
    }
}
