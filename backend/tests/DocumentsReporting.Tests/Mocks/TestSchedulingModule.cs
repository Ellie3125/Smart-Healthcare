using Scheduling.Contracts;

namespace DocumentsReporting.Tests.Mocks;

public sealed class TestSchedulingModule : ISchedulingModule
{
    public Func<Guid, Guid, Task<ClsQueueTicketValidationDto?>>? ValidateTicketHandler { get; set; }

    public Task<DoctorSearchPage> SearchDoctorsAsync(DoctorSearchQuery query, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<CheckInResult> CheckInAsync(CheckInAppointment command, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<ClsTicketsCreated> CreateClsTicketsAsync(CreateClsTickets command, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<ClsQueueTicketValidationDto?> ValidateClsTicketForRoomAsync(Guid queueTicketId, Guid roomId, CancellationToken ct = default)
    {
        if (ValidateTicketHandler != null)
            return ValidateTicketHandler(queueTicketId, roomId);

        return Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            queueTicketId,
            roomId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CLS-T01",
            "Nguyễn Văn A",
            new DateOnly(1990, 5, 12),
            IsServiceFinished: true,
            IsResultReturned: false));
    }
}
