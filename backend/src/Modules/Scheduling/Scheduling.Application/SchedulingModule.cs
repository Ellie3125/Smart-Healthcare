using Scheduling.Application.DoctorSearch;
using Scheduling.Contracts;

namespace Scheduling.Application;

/// <summary>Hiện thực ISchedulingModule (docs/api/scheduling.md §2). Check in thuộc W3-02, vé CLS thuộc W3-04.</summary>
public sealed class SchedulingModule(DoctorSearchService doctorSearch) : ISchedulingModule
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
        throw new NotSupportedException("Check in cài đặt ở W3-02.");

    public Task<ClsTicketsCreated> CreateClsTicketsAsync(CreateClsTickets command, CancellationToken ct) =>
        throw new NotSupportedException("Vé CLS và vé quay lại cài đặt ở W3-04.");
}
