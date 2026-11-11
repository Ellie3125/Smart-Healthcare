namespace Shared.Infrastructure;

/// <summary>
/// Chọn chuỗi kết nối theo cấu hình <c>Database:Source</c>. Máy dev đổi nguồn bằng launch profile:
/// <c>dotnet run</c> dùng SQL Server trên máy, <c>dotnet run -lp docker</c> dùng SQL Server trong Docker Compose.
/// Docker, Staging, Prod không đặt <c>Database:Source</c> nên luôn dùng <c>ConnectionStrings:SmartHealthcare</c>.
/// </summary>
public static class DatabaseSource
{
    public const string ConfigurationKey = "Database:Source";
    public const string Docker = "Docker";

    /// <summary>Tên chuỗi kết nối trong <c>ConnectionStrings</c> ứng với giá trị <c>Database:Source</c>.</summary>
    public static string ConnectionName(string? source) =>
        string.Equals(source, Docker, StringComparison.OrdinalIgnoreCase) ? "SmartHealthcareDocker" : "SmartHealthcare";
}
