using Shared.Infrastructure;
using Xunit;

namespace Shared.Infrastructure.Tests;

public sealed class DotEnvTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dotenv-").FullName;
    private readonly string _prefix = $"DOTENV_TEST_{Guid.NewGuid():N}_";

    public void Dispose()
    {
        foreach (var name in new[] { "A", "B", "C", "KEPT" })
            Environment.SetEnvironmentVariable(_prefix + name, null);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Load_reads_nearest_env_file_and_skips_comments()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "src", "Host")).FullName;
        File.WriteAllLines(Path.Combine(_root, ".env"),
        [
            "# chú thích",
            "",
            $"#{_prefix}A=bị comment",
            $"{_prefix}B=Server=.;Database=X;Password=a=b",
            $"  {_prefix}C = có khoảng trắng  ",
            "dòng không có dấu bằng",
        ]);

        var loaded = DotEnv.Load(nested);

        Assert.Equal(Path.Combine(_root, ".env"), loaded);
        Assert.Null(Environment.GetEnvironmentVariable(_prefix + "A"));
        Assert.Equal("Server=.;Database=X;Password=a=b", Environment.GetEnvironmentVariable(_prefix + "B"));
        Assert.Equal("có khoảng trắng", Environment.GetEnvironmentVariable(_prefix + "C"));
    }

    [Fact]
    public void Load_does_not_override_existing_environment_variable()
    {
        Environment.SetEnvironmentVariable(_prefix + "KEPT", "từ môi trường");
        File.WriteAllText(Path.Combine(_root, ".env"), $"{_prefix}KEPT=từ file");

        DotEnv.Load(_root);

        Assert.Equal("từ môi trường", Environment.GetEnvironmentVariable(_prefix + "KEPT"));
    }

    [Fact]
    public void Load_stops_at_backend_root_when_no_env_file()
    {
        // .env nằm trên gốc backend thì không được nạp.
        var backend = Directory.CreateDirectory(Path.Combine(_root, "backend")).FullName;
        File.WriteAllText(Path.Combine(backend, "SmartHealthcare.slnx"), "");
        File.WriteAllText(Path.Combine(_root, ".env"), $"{_prefix}A=ngoài backend");

        Assert.Null(DotEnv.Load(backend));
        Assert.Null(Environment.GetEnvironmentVariable(_prefix + "A"));
    }
}
