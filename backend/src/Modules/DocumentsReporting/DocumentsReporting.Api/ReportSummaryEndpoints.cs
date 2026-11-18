using System.Globalization;
using DocumentsReporting.Application.Reports;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace DocumentsReporting.Api;

internal static class ReportSummaryEndpoints
{
    public static void MapReportSummary(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/documents/admin/reports/summary", async (string? dateFrom, string? dateTo,
            [FromServices] ReportSummaryService service, CancellationToken ct) =>
        {
            if (!DateOnly.TryParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                || !DateOnly.TryParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                return InvalidInput();
            try { return (IResult)TypedResults.Ok(await service.ReadAsync(from, to, ct)); }
            catch (ArgumentException) { return InvalidInput(); }
        }).RequireAuthorization(Policies.AdminOnly).WithTags(DependencyInjection.Tag)
            .Produces<ReportSnapshot>().ProducesProblem(400).Produces(401).Produces(403);

    private static IResult InvalidInput() => TypedResults.Problem(statusCode: 400, title: "InvalidInput",
        extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
}
