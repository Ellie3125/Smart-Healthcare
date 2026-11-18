using Scheduling.Application.DoctorSearch;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Clinical.Contracts;

namespace Scheduling.Application;

/// <summary>Hiện thực ISchedulingModule (docs/api/scheduling.md §2). Check in thuộc W3-02, vé CLS thuộc W3-04.</summary>
public sealed class SchedulingModule(DoctorSearchService doctorSearch, Func<CheckInService> checkIn,
    Func<ClsTicketService>? clsTickets = null, Func<IClsTicketReader>? clsTicketReader = null,
    Func<IClinicalModule>? clinical = null) : ISchedulingModule
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
        (clsTickets ?? throw new NotSupportedException("Host chưa đăng ký ClsTicketService."))().CreateAsync(command, ct);

    public async Task<ClsQueueTicketValidationDto?> ValidateClsTicketForRoomAsync(Guid queueTicketId, Guid roomId, CancellationToken ct = default)
    {
        if (queueTicketId == Guid.Empty || roomId == Guid.Empty || clsTicketReader is null || clinical is null) return null;
        var ticket = await clsTicketReader().GetByIdAsync(queueTicketId, ct);
        if (ticket is null || ticket.RoomId != roomId) return null;
        var module = clinical();
        var encounter = await module.GetEncounterByIdAsync(ticket.EncounterId, ct);
        if (encounter is null) return null;
        var orders = await module.GetServiceOrdersByEncounterIdAsync(ticket.EncounterId, ct);
        var item = orders.Where(o => o.EncounterId == ticket.EncounterId).SelectMany(o => o.Items).SingleOrDefault(i => i.Id == ticket.ServiceOrderItemId);
        if (item?.Result is not { } result || result.ServiceOrderItemId != item.Id) return null;
        return new(queueTicketId, roomId, ticket.EncounterId, encounter.PatientId, item.Id, result.Id, ticket.TicketNumber, null, null,
            ticket.QueueStatus == "Finished" && item.ItemStatus is "ServiceFinished" or "Completed", result.ResultStatus == "Published");
    }
}
