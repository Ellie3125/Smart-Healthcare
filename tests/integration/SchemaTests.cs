using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SmartHealthcare.Identity.Infrastructure.Persistence;
using SmartHealthcare.Patients.Infrastructure.Persistence;

namespace SmartHealthcare.IntegrationTests;

/// <summary>
/// Schema do migration của backend/src/Database tạo; DbContext của từng module map lại cùng bảng,
/// nên phải khớp từng cột với database mà migration sinh ra.
/// </summary>
[Collection(ApiCollection.Name)]
public class SchemaTests(ApiFactory api)
{
    [Fact]
    public async Task Migrations_create_all_46_tables_of_erd_v2_2_and_record_history()
    {
        Assert.Equal(46, await api.ScalarAsync(
            "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND SCHEMA_NAME(schema_id) = 'dbo' AND name <> '__EFMigrationsHistory'"));

        await using var schema = api.CreateSchemaContext();
        Assert.Empty(await schema.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public void Schema_model_has_no_changes_without_a_migration()
    {
        // Sửa mô hình trong backend/src/Database mà quên "dotnet ef migrations add" thì test này đỏ.
        using var schema = api.CreateSchemaContext();
        Assert.False(schema.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Ef_mapping_matches_every_mapped_column_in_the_database()
    {
        using var scope = api.Services.CreateScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>(),
            scope.ServiceProvider.GetRequiredService<PatientsModuleDbContext>(),
        ];

        var mismatches = new List<string>();
        foreach (var context in contexts)
        foreach (var entity in context.GetService<IDesignTimeModel>().Model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(table)!;
                var expected = $"{property.GetColumnType(table).ToLowerInvariant()} {(property.IsColumnNullable(table) ? "null" : "not null")}";
                var actual = await DescribeColumnAsync(table.Schema!, table.Name, column);
                if (actual != expected) mismatches.Add($"{table.Schema}.{table.Name}.{column}: EF '{expected}', DB '{actual ?? "không có cột"}'");
            }
        }

        Assert.Empty(mismatches);
    }

    private async Task<string?> DescribeColumnAsync(string schema, string table, string column)
    {
        await using var connection = new SqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, DATETIME_PRECISION, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t AND COLUMN_NAME = @c
            """, connection);
        command.Parameters.AddWithValue("@s", schema);
        command.Parameters.AddWithValue("@t", table);
        command.Parameters.AddWithValue("@c", column);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var dataType = reader.GetString(0);
        var type = dataType switch
        {
            "nvarchar" or "varchar" or "char" or "nchar" => $"{dataType}({(reader.GetInt32(1) == -1 ? "max" : reader.GetInt32(1))})",
            "datetime2" => $"datetime2({reader.GetInt16(2)})",
            "timestamp" => "rowversion",
            _ => dataType,
        };
        return $"{type} {(reader.GetString(3) == "YES" ? "null" : "not null")}";
    }
}
