using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.DoctorSearch;

namespace Scheduling.Api;

internal static class DoctorSearchEndpoints
{
    public static void MapDoctorSearchEndpoints(this RouteGroupBuilder scheduling)
    {
        scheduling.MapGet("/patient/doctors", SearchAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Patient))
            .WithSummary("Tìm bác sĩ, suất trống và phí khám (Patient)")
            .Produces<DoctorSearchPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> SearchAsync([AsParameters] DoctorSearchQuery query,
        [FromServices] DoctorSearchService service, CancellationToken ct)
    {
        if (query.ServiceDate == DateOnly.MinValue || query.DoctorRankId == Guid.Empty || query.SpecialtyId == Guid.Empty
            || query.Page is < 1 or > DoctorSearchQuery.MaxPage || query.PageSize is < 1 or > DoctorSearchQuery.MaxPageSize)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Ngày, bộ lọc hoặc phân trang không hợp lệ.");
        }

        var page = await service.SearchAsync(query, ct);
        return page is not null
            ? TypedResults.Ok(page)
            : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Chưa thể báo giá phí khám theo lô.",
                extensions: new Dictionary<string, object?> { ["code"] = "ExamFeeBatchUnavailable" });
    }
}
