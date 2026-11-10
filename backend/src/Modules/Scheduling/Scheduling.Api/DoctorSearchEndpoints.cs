using System.Globalization;
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
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> SearchAsync(string? serviceDate, string? doctorRankId, string? specialtyId,
        [FromServices] DoctorSearchService service, CancellationToken ct, string page = "1", string pageSize = "20")
    {
        var rank = Guid.Empty;
        var specialty = Guid.Empty;
        if (!DateOnly.TryParseExact(serviceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date == DateOnly.MinValue
            || (doctorRankId is not null && (!Guid.TryParse(doctorRankId, out rank) || rank == Guid.Empty))
            || (specialtyId is not null && (!Guid.TryParse(specialtyId, out specialty) || specialty == Guid.Empty))
            || !int.TryParse(page, CultureInfo.InvariantCulture, out var pageNumber)
            || pageNumber is < 1 or > DoctorSearchQuery.MaxPage
            || !int.TryParse(pageSize, CultureInfo.InvariantCulture, out var size)
            || size is < 1 or > DoctorSearchQuery.MaxPageSize)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Ngày, bộ lọc hoặc phân trang không hợp lệ.",
                extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
        }

        var query = new DoctorSearchQuery(date, doctorRankId is null ? null : rank, specialtyId is null ? null : specialty,
            pageNumber, size);
        return TypedResults.Ok(await service.SearchAsync(query, ct));
    }
}
