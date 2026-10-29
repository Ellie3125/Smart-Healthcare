using Microsoft.AspNetCore.Http;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Identity.Presentation;

/// <summary>Đổi lỗi use case thành ProblemDetails (RFC 9457) kèm mã lỗi ổn định cho client.</summary>
internal static class ErrorResults
{
    public static IResult From(Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Kind == ErrorKind.Validation && error.Fields is not null)
            return Results.ValidationProblem(error.Fields.ToDictionary(), detail: error.Message, extensions: extensions);

        var status = error.Kind switch
        {
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(detail: error.Message, statusCode: status, extensions: extensions);
    }
}
