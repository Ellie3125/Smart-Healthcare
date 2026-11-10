namespace IdentityPatients.Application.Common;

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorDetail { get; private init; }
    public IDictionary<string, object?>? Extensions { get; private init; }

    public static ServiceResult<T> Success(T value, int statusCode = 200) =>
        new() { IsSuccess = true, Value = value, StatusCode = statusCode };

    public static ServiceResult<T> Failure(int statusCode, string title, string detail, IDictionary<string, object?>? extensions = null) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail, Extensions = extensions };
}

public sealed class ServiceResult
{
    public bool IsSuccess { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorDetail { get; private init; }
    public IDictionary<string, object?>? Extensions { get; private init; }

    public static ServiceResult Success(int statusCode = 200) =>
        new() { IsSuccess = true, StatusCode = statusCode };

    public static ServiceResult Failure(int statusCode, string title, string detail, IDictionary<string, object?>? extensions = null) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail, Extensions = extensions };
}
