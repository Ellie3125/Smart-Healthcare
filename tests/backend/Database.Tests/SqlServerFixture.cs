using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Tests;

/// <summary>
/// Tạo DB tạm trên SQL Server thật (biến môi trường SH_TEST_SQL_CONNECTION trỏ tới server, không chứa Database),
/// apply migration của 6 module rồi xóa DB khi xong.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string? _server = Environment.GetEnvironmentVariable("SH_TEST_SQL_CONNECTION");
    private readonly string _database = $"ShTest_{Guid.NewGuid():N}";

    public string ConnectionString => new SqlConnectionStringBuilder(_server) { InitialCatalog = _database }.ConnectionString;

    public bool Available => !string.IsNullOrWhiteSpace(_server);

    public T Context<T>(string domain) where T : DbContext => (T)Contexts.Create(domain, ConnectionString);

    public async ValueTask InitializeAsync()
    {
        if (!Available) return;
        foreach (var domain in Contexts.Domains)
        {
            await using var context = Contexts.Create(domain, ConnectionString);
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!Available) return;
        await using var connection = new SqlConnection(_server);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"IF DB_ID(N'{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
