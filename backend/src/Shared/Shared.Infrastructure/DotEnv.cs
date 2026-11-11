namespace Shared.Infrastructure;

/// <summary>
/// Nạp file <c>.env</c> của máy dev vào biến môi trường của tiến trình, trước khi host đọc cấu hình.
/// Nhờ đó một dòng trong <c>backend/.env</c> thắng user-secrets; biến môi trường đã đặt sẵn (Docker, Staging, Prod) không bị ghi đè.
/// </summary>
public static class DotEnv
{
    private const string FileName = ".env";

    // Gốc backend: không tìm .env cao hơn thư mục này.
    private const string RootMarker = "SmartHealthcare.slnx";

    /// <summary>Tìm <c>.env</c> từ <paramref name="startDirectory"/> (mặc định: thư mục hiện tại) ngược lên tới gốc backend.</summary>
    /// <returns>Đường dẫn file đã nạp, hoặc null nếu không có.</returns>
    public static string? Load(string? startDirectory = null)
    {
        for (var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, FileName);
            if (File.Exists(path))
            {
                Apply(File.ReadLines(path));
                return path;
            }

            if (File.Exists(Path.Combine(dir.FullName, RootMarker)))
                break;
        }

        return null;
    }

    private static void Apply(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var separator = line.IndexOf('=');

            // Dòng trống, dòng chú thích (#) và dòng không có KEY= đều bỏ qua. Giá trị giữ nguyên phần sau dấu = đầu tiên.
            if (line.StartsWith('#') || separator <= 0)
                continue;

            var key = line[..separator].Trim();
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, line[(separator + 1)..].Trim());
        }
    }
}
