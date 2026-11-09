namespace Scheduling.Application.DoctorSearch;

// W2-05 phần 1: chuyển DTO sang Scheduling.Contracts sau W1-04.
public sealed record DoctorSearchQuery(DateOnly ServiceDate, Guid? DoctorRankId = null, Guid? SpecialtyId = null,
    int Page = 1, int PageSize = 20)
{
    public const int MaxPage = 1000;
    public const int MaxPageSize = 50;
}

public sealed record DoctorSearchItem(Guid DoctorId, string FullName, Guid DoctorRankId, Guid SpecialtyId,
    int AvailableSlots, decimal ExamFee, string Currency);

public sealed record DoctorSearchPage(DateOnly ServiceDate, int Page, int PageSize, bool HasMore,
    IReadOnlyList<DoctorSearchItem> Items);
