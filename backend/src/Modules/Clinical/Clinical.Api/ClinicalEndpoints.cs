using Clinical.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

internal static class ClinicalEndpoints
{
    public static RouteGroupBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clinical").WithTags("Clinical");

        // 1. Tiếp nhận bệnh nhân check-in tạo lượt khám (Encounter)
        group.MapPost("/encounters/check-in", async (
            [FromBody] StartEncounterCommand command,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var encounter = await clinical.StartEncounterAsync(command, ct);
                return Results.Created($"/api/clinical/encounters/{encounter.Id}", encounter);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.ActiveEncounterAlreadyExists)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Active Encounter Already Exists",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                    Extensions = { ["errorCode"] = ex.Code.ToString() }
                });
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                    Extensions = { ["errorCode"] = ex.Code.ToString() }
                });
            }
        })
        .WithName("CheckInEncounter")
        .WithSummary("Tiếp nhận bệnh nhân từ lịch hẹn và khởi tạo lượt khám (Encounter InProgress)")
        .Produces<EncounterDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 2. Lấy thông tin chi tiết lượt khám theo EncounterId
        group.MapGet("/encounters/{id:guid}", async (
            Guid id,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var encounter = await clinical.GetEncounterByIdAsync(id, ct);
                return encounter is null
                    ? Results.NotFound(new ProblemDetails
                    {
                        Title = "Encounter Not Found",
                        Detail = $"Không tìm thấy lượt khám với ID {id}.",
                        Status = StatusCodes.Status404NotFound
                    })
                    : Results.Ok(encounter);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("GetEncounterById")
        .WithSummary("Lấy thông tin chi tiết một lượt khám theo EncounterId")
        .Produces<EncounterDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. Kiểm tra bệnh nhân có lượt khám nào đang hoạt động (InProgress, WaitingCLS, ReturningDoctor)
        group.MapGet("/encounters/active-by-patient/{patientId:guid}", async (
            Guid patientId,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var hasActive = await clinical.HasActiveEncounterAsync(patientId, ct);
                return Results.Ok(new ActiveEncounterCheckResponse(patientId, hasActive));
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("CheckActiveEncounterByPatient")
        .WithSummary("Kiểm tra xem bệnh nhân có đang có lượt khám nào dở dang hay không")
        .Produces<ActiveEncounterCheckResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 4. Lấy lịch sử tất cả các lượt khám của một bệnh nhân
        group.MapGet("/encounters/patient/{patientId:guid}", async (
            Guid patientId,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var history = await clinical.GetPatientEncountersAsync(patientId, ct);
                return Results.Ok(history);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("GetPatientEncounters")
        .WithSummary("Lấy danh sách tóm tắt toàn bộ lịch sử các lượt khám của bệnh nhân")
        .Produces<IReadOnlyList<EncounterSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 5. Lấy hồ sơ bệnh án lâm sàng của lượt khám
        group.MapGet("/encounters/{id:guid}/record", async (
            Guid id,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var record = await clinical.GetClinicalRecordByEncounterIdAsync(id, ct);
                return record is null
                    ? Results.NotFound(new ProblemDetails
                    {
                        Title = "Clinical Record Not Found",
                        Detail = $"Không tìm thấy hồ sơ bệnh án cho lượt khám {id}.",
                        Status = StatusCodes.Status404NotFound
                    })
                    : Results.Ok(record);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("GetEncounterClinicalRecord")
        .WithSummary("Lấy thông tin hồ sơ bệnh án lâm sàng của một lượt khám")
        .Produces<EncounterClinicalRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 6. Lấy danh sách phiếu chỉ định cận lâm sàng của lượt khám
        group.MapGet("/encounters/{id:guid}/service-orders", async (
            Guid id,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var orders = await clinical.GetServiceOrdersByEncounterIdAsync(id, ct);
                return Results.Ok(orders);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("GetEncounterServiceOrders")
        .WithSummary("Lấy danh sách tất cả các phiếu chỉ định cận lâm sàng (CLS) của lượt khám")
        .Produces<IReadOnlyList<ServiceOrderDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 7. Lấy danh sách đơn thuốc của lượt khám
        group.MapGet("/encounters/{id:guid}/prescriptions", async (
            Guid id,
            [FromServices] IClinicalModule clinical,
            CancellationToken ct) =>
        {
            try
            {
                var prescriptions = await clinical.GetPrescriptionsByEncounterIdAsync(id, ct);
                return Results.Ok(prescriptions);
            }
            catch (ClinicalException ex) when (ex.Code == ClinicalErrorCode.InvalidRequest)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        })
        .WithName("GetEncounterPrescriptions")
        .WithSummary("Lấy danh sách tất cả các đơn thuốc của lượt khám")
        .Produces<IReadOnlyList<PrescriptionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}

public sealed record ActiveEncounterCheckResponse(Guid PatientId, bool HasActiveEncounter);
