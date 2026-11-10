namespace IdentityPatients.Application.Common;

/// <summary>Mã lỗi (extension <c>code</c>, docs/conventions.md §4) khi không truyền mã riêng.</summary>
public static class ErrorCodes
{
    public const string InvalidInput = "InvalidInput";
    public const string NotFound = "NotFound";
    public const string Conflict = "Conflict";
    public const string Forbidden = "Forbidden";

    public static string ForStatus(int statusCode) => statusCode switch
    {
        400 => InvalidInput,
        403 => Forbidden,
        404 => NotFound,
        409 => Conflict,
        _ => "Error"
    };
}

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorDetail { get; private init; }
    public string? ErrorCode { get; private init; }
    public IDictionary<string, object?>? Extensions { get; private init; }

    public static ServiceResult<T> Success(T value, int statusCode = 200) =>
        new() { IsSuccess = true, Value = value, StatusCode = statusCode };

    public static ServiceResult<T> Failure(int statusCode, string title, string detail, IDictionary<string, object?>? extensions = null, string? code = null) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail, Extensions = extensions, ErrorCode = code ?? ErrorCodes.ForStatus(statusCode) };
}

public sealed class ServiceResult
{
    public bool IsSuccess { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorDetail { get; private init; }
    public string? ErrorCode { get; private init; }
    public IDictionary<string, object?>? Extensions { get; private init; }

    public static ServiceResult Success(int statusCode = 200) =>
        new() { IsSuccess = true, StatusCode = statusCode };

    public static ServiceResult Failure(int statusCode, string title, string detail, IDictionary<string, object?>? extensions = null, string? code = null) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail, Extensions = extensions, ErrorCode = code ?? ErrorCodes.ForStatus(statusCode) };
}
