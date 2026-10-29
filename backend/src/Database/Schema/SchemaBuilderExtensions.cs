using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SmartHealthcare.Database.Schema;

/// <summary>
/// Khai báo bảng dạng property bag (không cần lớp entity) và đặt tên ràng buộc theo một quy ước:
/// PK_Bảng, FK_Bảng_BảngCha, UQ_Bảng_Cột, CK_Bảng_Cột, DF_Bảng_Cột, IX_Bảng_Cột.
/// </summary>
internal static class SchemaBuilderExtensions
{
    public static void Table(this ModelBuilder model, string name, Action<EntityTypeBuilder> build)
    {
        var table = model.SharedTypeEntity<Dictionary<string, object>>(name);
        table.ToTable(name, "dbo");
        build(table);
    }

    /// <summary>Khóa chính Id kiểu uniqueidentifier, mặc định NEWSEQUENTIALID(); ứng dụng thường tự sinh Guid v7.</summary>
    public static void Id(this EntityTypeBuilder table)
    {
        table.Req<Guid>("Id", "uniqueidentifier").Default("NEWSEQUENTIALID()");
        table.HasKey("Id").HasName($"PK_{table.Metadata.Name}");
    }

    public static PropertyBuilder Req<T>(this EntityTypeBuilder table, string column, string sqlType) where T : notnull =>
        table.IndexerProperty(typeof(T), column).HasColumnType(sqlType).IsRequired();

    public static PropertyBuilder Opt<T>(this EntityTypeBuilder table, string column, string sqlType) =>
        table.IndexerProperty(typeof(T).IsValueType ? typeof(Nullable<>).MakeGenericType(typeof(T)) : typeof(T), column)
            .HasColumnType(sqlType).IsRequired(false);

    public static PropertyBuilder Default(this PropertyBuilder column, string sql) =>
        column.HasDefaultValueSql(sql, $"DF_{column.Metadata.DeclaringType.Name}_{column.Metadata.Name}");

    /// <summary>Cột nvarchar chỉ nhận các giá trị liệt kê trên sơ đồ.</summary>
    public static PropertyBuilder Enum(this EntityTypeBuilder table, string column, int length, params string[] values)
    {
        table.Rule(column, $"{column} IN ({string.Join(", ", values.Select(v => $"N'{v}'"))})");
        return table.Req<string>(column, $"nvarchar({length})");
    }

    public static void Flag(this EntityTypeBuilder table, string column, bool defaultValue) =>
        table.Req<bool>(column, "bit").Default(defaultValue ? "1" : "0");

    public static PropertyBuilder Utc(this EntityTypeBuilder table, string column) => table.Req<DateTime>(column, "datetime2(3)");

    public static PropertyBuilder OptUtc(this EntityTypeBuilder table, string column) => table.Opt<DateTime>(column, "datetime2(3)");

    public static void UtcNow(this EntityTypeBuilder table, string column) => table.Utc(column).Default("SYSUTCDATETIME()");

    public static PropertyBuilder Money(this EntityTypeBuilder table, string column) => table.Req<decimal>(column, "decimal(18,2)");

    public static void Currency(this EntityTypeBuilder table)
    {
        table.Req<string>("Currency", "char(3)").Default("'VND'");
        table.Rule("Currency", "Currency = 'VND'");
    }

    public static void RowVersion(this EntityTypeBuilder table) =>
        table.IndexerProperty<byte[]>("RowVersion").IsRowVersion().IsRequired();

    /// <summary>Cột FK bắt buộc. Tên ràng buộc FK_Bảng_{name ?? bảng cha}; không cascade.</summary>
    public static void Ref(this EntityTypeBuilder table, string column, string principal, string? name = null)
    {
        table.Req<Guid>(column, "uniqueidentifier");
        table.Relate(column, principal, name);
    }

    public static void OptRef(this EntityTypeBuilder table, string column, string principal, string? name = null)
    {
        table.Opt<Guid>(column, "uniqueidentifier");
        table.Relate(column, principal, name);
    }

    public static void Unique(this EntityTypeBuilder table, string column) =>
        table.HasAlternateKey(column).HasName($"UQ_{table.Metadata.Name}_{column}");

    public static void UniqueKey(this EntityTypeBuilder table, string name, params string[] columns) =>
        table.HasAlternateKey(columns).HasName(name);

    /// <summary>Unique index chỉ áp cho các dòng thỏa điều kiện (filtered), ví dụ cột có giá trị hoặc dòng đang hoạt động.</summary>
    public static void UniqueWhere(this EntityTypeBuilder table, string name, string filter, params string[] columns) =>
        table.HasIndex(columns, name).IsUnique().HasFilter(filter);

    public static void Index(this EntityTypeBuilder table, string name, string? filter, params string[] columns) =>
        table.HasIndex(columns, name).HasFilter(filter);

    /// <summary>CHECK đặt tên CK_Bảng_Cột.</summary>
    public static void Rule(this EntityTypeBuilder table, string column, string sql) =>
        table.Check($"CK_{table.Metadata.Name}_{column}", sql);

    public static void Check(this EntityTypeBuilder table, string name, string sql) =>
        table.ToTable(table.Metadata.Name, "dbo", t => t.HasCheckConstraint(name, sql));

    private static void Relate(this EntityTypeBuilder table, string column, string principal, string? name) =>
        table.HasOne(principal, null).WithMany().HasForeignKey(column)
            .HasConstraintName($"FK_{table.Metadata.Name}_{name ?? principal}")
            .OnDelete(DeleteBehavior.NoAction);
}
