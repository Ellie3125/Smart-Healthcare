using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SqlServerTests;

// Kết nối tới DB test riêng SmartHealthcare_Test (giữ lâu dài, tạo bằng migration); mọi thay đổi nằm trong một transaction bị rollback.
public sealed class TestDatabase : IAsyncDisposable
{
    public const string DatabaseName = "SmartHealthcare_Test";
    public const int CheckViolation = 547;
    public const int UniqueIndexViolation = 2601;

    private static readonly string? ConnectionString =
        new ConfigurationBuilder().AddUserSecrets<TestDatabase>(optional: true).Build().GetConnectionString("SmartHealthcareTest")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__SmartHealthcareTest");

    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;

    private TestDatabase(SqlConnection connection, SqlTransaction transaction) =>
        (_connection, _transaction) = (connection, transaction);

    /// <summary>Connection string tới DB test; bỏ qua test khi chưa đặt, báo lỗi khi trỏ sang DB khác.</summary>
    public static string RequiredConnectionString()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), "Chưa đặt ConnectionStrings:SmartHealthcareTest (user-secrets của project test)");

        // Chặn chạy nhầm lên DB khác, nhất là DB SmartHealthcare.
        if (new SqlConnectionStringBuilder(ConnectionString).InitialCatalog != DatabaseName)
        {
            throw new InvalidOperationException($"ConnectionStrings:SmartHealthcareTest phải trỏ tới {DatabaseName}.");
        }

        return ConnectionString!;
    }

    public static async Task<TestDatabase> OpenAsync()
    {
        var connection = new SqlConnection(RequiredConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        if (connection.Database != DatabaseName)
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException($"Đã kết nối tới {connection.Database}, không phải {DatabaseName}.");
        }

        return new TestDatabase(connection, (SqlTransaction)await connection.BeginTransactionAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Chèn một dòng; cột NOT NULL không truyền vào được điền giá trị mặc định theo kiểu. Trả về Id.</summary>
    public async Task<Guid> InsertAsync(string table, object? values = null)
    {
        var row = (values?.GetType().GetProperties() ?? []).ToDictionary(p => p.Name, p => p.GetValue(values));
        foreach (var (column, type) in await RequiredColumnsAsync(table))
        {
            if (!row.ContainsKey(column)) row[column] = DefaultValue(column, type);
        }

        await using var command = Command(
            $"INSERT INTO {string.Join(".", table.Split('.').Select(part => $"[{part}]"))} ({string.Join(", ", row.Keys.Select(k => $"[{k}]"))}) VALUES ({string.Join(", ", row.Keys.Select(k => $"@{k}"))})");
        foreach (var (column, value) in row)
        {
            command.Parameters.AddWithValue($"@{column}", value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return row.TryGetValue("Id", out var id) && id is Guid guid ? guid : Guid.Empty;
    }

    /// <summary>Lệnh phải bị SQL Server từ chối với đúng mã lỗi.</summary>
    public async Task RejectsAsync(int errorNumber, string table, object values)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => InsertAsync(table, values));
        Assert.Equal(errorNumber, error.Number);
    }

    public async Task<List<string>> QueryAsync(string sql)
    {
        await using var command = Command(sql);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue)));
        }

        return rows;
    }

    /// <summary>
    /// DbContext dùng chung kết nối và transaction của test, nên dữ liệu EF ghi cũng bị rollback khi dispose.
    /// </summary>
    public TContext CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext> create) where TContext : DbContext
    {
        var context = create(new DbContextOptionsBuilder<TContext>().UseSqlServer(_connection).Options);
        context.Database.UseTransaction(_transaction);
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _connection.DisposeAsync();
    }

    private SqlCommand Command(string sql) => new(sql, _connection, _transaction);

    private async Task<List<(string Column, string Type)>> RequiredColumnsAsync(string table)
    {
        await using var command = Command("""
            SELECT c.name, t.name
            FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@table) AND c.is_nullable = 0 AND c.is_computed = 0 AND t.name <> 'timestamp'
            """);
        command.Parameters.Add("@table", SqlDbType.NVarChar, 256).Value = table;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<(string, string)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        Assert.True(columns.Count > 0, $"Không tìm thấy bảng {table}");
        return columns;
    }

    // Chuỗi ngẫu nhiên 12 ký tự: vừa mọi cột (ngắn nhất nvarchar(32)) và không đụng unique key.
    private static object DefaultValue(string column, string type) => type switch
    {
        "uniqueidentifier" => Guid.NewGuid(),
        "nvarchar" => Guid.NewGuid().ToString("N")[..12],
        "int" or "bigint" or "smallint" or "tinyint" => 0,
        "decimal" => 0m,
        "bit" => false,
        "datetime2" => DateTime.UtcNow,
        "date" => DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(TimeOnly.MinValue),
        _ => throw new NotSupportedException($"Chưa có giá trị mặc định cho cột {column} kiểu {type}"),
    };
}
