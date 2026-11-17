using Scheduling.Application.DoctorSearch;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;

namespace Scheduling.Application;

/// <summary>Hiện thực ISchedulingModule (docs/api/scheduling.md §2). Check in thuộc W3-02, vé CLS thuộc W3-04.</summary>
public sealed class SchedulingModule(DoctorSearchService doctorSearch, Func<CheckInService> checkIn) : ISchedulingModule
{
    public Task<DoctorSearchPage> SearchDoctorsAsync(DoctorSearchQuery query, CancellationToken ct)
    {
        if (query is null
            || query.ServiceDate == default
            || query.DoctorRankId == Guid.Empty
            || query.SpecialtyId == Guid.Empty
            || query.Page is < 1 or > DoctorSearchQuery.MaxPage
            || query.PageSize is < 1 or > DoctorSearchQuery.MaxPageSize)
        {
            throw new SchedulingException(SchedulingErrorCode.InvalidInput, "Ngày, bộ lọc hoặc phân trang không hợp lệ.");
        }

        return doctorSearch.SearchAsync(query, ct);
    }

    public Task<CheckInResult> CheckInAsync(CheckInAppointment command, CancellationToken ct) =>
        checkIn().CheckInAsync(command, ct);

    public Task<ClsTicketsCreated> CreateClsTicketsAsync(CreateClsTickets command, CancellationToken ct) =>
        throw new NotSupportedException("Vé CLS và vé quay lại cài đặt ở W3-04.");

    public Func<Guid, Guid, Task<ClsQueueTicketValidationDto?>>? ValidateClsTicketHandler { get; set; }

    public Task<ClsQueueTicketValidationDto?> ValidateClsTicketForRoomAsync(Guid queueTicketId, Guid roomId, CancellationToken ct = default)
    {
        if (ValidateClsTicketHandler != null)
            return ValidateClsTicketHandler(queueTicketId, roomId);

        // Fallback mặc định khi Hòa chưa triển khai bảng QueueTicket (W3-02/W3-04)
        return Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            queueTicketId,
            roomId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CLS-001",
            "Nguyễn Văn A",
            new DateOnly(1990, 1, 1),
            IsServiceFinished: true,
            IsResultReturned: false));
    }
}
