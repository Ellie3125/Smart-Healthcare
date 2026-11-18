using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Application.MedicalServiceAdmin;

namespace Scheduling.Api;

internal static class MedicalServiceAdminEndpoints
{
    public static void MapMedicalServiceCatalogRoutes(this RouteGroupBuilder scheduling)
    {
        var admin = scheduling.MapGroup("/admin").RequireAuthorization("AdminOnly").AddEndpointFilter(Errors);
        admin.MapGet("/medical-services", (int? page, int? pageSize, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
            catalog.GetServicesAsync(false, page ?? 1, pageSize ?? 50, ct)).WithSummary("Danh mục dịch vụ CLS (Staff Admin)");
        admin.MapPost("/medical-services", async (CreateMedicalService request, ClaimsPrincipal user, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
        {
            var result = await catalog.CreateServiceAsync(request, UserId(user), ct);
            return TypedResults.Created((string?)null, result);
        }).WithSummary("Tạo dịch vụ CLS và phòng mặc định (Staff Admin)");
        admin.MapPut("/medical-services/{id:guid}", (Guid id, UpdateMedicalService request, ClaimsPrincipal user, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
            catalog.UpdateServiceAsync(id, request, UserId(user), ct)).WithSummary("Sửa dịch vụ CLS (Staff Admin)");
        admin.MapPut("/medical-services/{id:guid}/rooms/{roomId:guid}", async (Guid id, Guid roomId, RoomMappingRequest request,
            ClaimsPrincipal user, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
        {
            await catalog.SetRoomAsync(id, roomId, request.IsActive, UserId(user), ct);
            return TypedResults.NoContent();
        }).WithSummary("Bật/tắt phòng thực hiện dịch vụ CLS (Staff Admin)");
        admin.MapGet("/service-stations", (Guid? roomId, int? page, int? pageSize, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
            catalog.GetStationsAsync(roomId, page ?? 1, pageSize ?? 50, ct)).WithSummary("Danh mục vị trí phục vụ (Staff Admin)");
        admin.MapPost("/service-stations", async (CreateServiceStation request, ClaimsPrincipal user, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
        {
            var result = await catalog.CreateStationAsync(request, UserId(user), ct);
            return TypedResults.Created((string?)null, result);
        }).WithSummary("Tạo vị trí phục vụ CLS (Staff Admin)");
        admin.MapPut("/service-stations/{id:guid}", (Guid id, UpdateServiceStation request, ClaimsPrincipal user, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
            catalog.UpdateStationAsync(id, request, UserId(user), ct)).WithSummary("Sửa hoặc bật/tắt vị trí CLS (Staff Admin)");

        scheduling.MapGet("/staff/medical-services", (int? page, int? pageSize, [FromServices] IMedicalServiceCatalog catalog, CancellationToken ct) =>
            catalog.GetServicesAsync(true, page ?? 1, pageSize ?? 50, ct))
            .RequireAuthorization("DoctorOnly").AddEndpointFilter(Errors)
            .WithSummary("Dịch vụ CLS đang hoạt động để chỉ định, không kèm giá (Staff Doctor)");
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException();

    private static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return TypedResults.Unauthorized(); }
        catch (CatalogException error)
        {
            var status = error.Error switch
            {
                CatalogError.InvalidInput => 400,
                CatalogError.RoomNotFound or CatalogError.ServiceNotFound or CatalogError.StationNotFound => 404,
                CatalogError.CodeAlreadyExists or CatalogError.DefaultRoomRequired => 409,
                _ => throw new InvalidOperationException($"Unexpected catalog error {error.Error}")
            };
            return TypedResults.Problem(statusCode: status, title: error.Error.ToString(),
                extensions: new Dictionary<string, object?> { ["code"] = error.Error.ToString() });
        }
    }

    private sealed record RoomMappingRequest(bool IsActive);
}
