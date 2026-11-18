using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using IdentityPatients.Contracts;
using MedicationCatalog.Application;
using Microsoft.AspNetCore.Mvc;

namespace MedicationCatalog.Api;

public static class DependencyInjection
{
    public const string Tag = "MedicationCatalog";

    public static IServiceCollection AddMedicationCatalogApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapMedicationCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/medication-catalog").WithTags(Tag);
        group.MapGet("/staff/drugs", async (string? q, [FromServices] IDrugCatalog catalog, CancellationToken ct) =>
        {
            if (q is { Length: > 100 } || q?.Any(char.IsControl) == true)
                return (IResult)TypedResults.Problem(statusCode: 400, title: "InvalidInput",
                    extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
            return TypedResults.Ok(await catalog.SearchAsync(q?.Trim() ?? "", ct));
        }).RequireAuthorization(Policies.DoctorOnly).Produces<IReadOnlyList<DrugCatalogItem>>()
            .ProducesProblem(400).Produces(401).Produces(403);
        return group;
    }
}
