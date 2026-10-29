using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SmartHealthcare.Database.Tests;

/// <summary>Đối chiếu model EF của từng module với ERD v2.2; gom mọi sai lệch để báo một lần.</summary>
public sealed class ErdModelTests
{
    public static TheoryData<string> Domains => [.. Contexts.Domains];

    [Fact]
    public void Erd_has_46_tables_in_6_domains() =>
        Assert.Equal(46, ErdSpec.Entities.Count);

    [Theory]
    [MemberData(nameof(Domains))]
    public void Model_matches_erd(string domain)
    {
        using var context = Contexts.Create(domain, "Server=model-only");
        var model = context.GetService<IDesignTimeModel>().Model;
        var errors = new List<string>();
        var expected = ErdSpec.Entities.Where(e => e.Value.Domain == domain).ToDictionary(e => e.Key, e => e.Value.Columns);

        var actualTables = model.GetEntityTypes().Select(e => e.GetTableName()!).ToHashSet();
        errors.AddRange(actualTables.Except(expected.Keys).Select(t => $"Bảng thừa: {t}"));

        foreach (var (table, columns) in expected)
        {
            var entity = model.GetEntityTypes().SingleOrDefault(e => e.GetTableName() == table);
            if (entity is null) { errors.Add($"Thiếu bảng {table}"); continue; }
            CheckTable(domain, table, columns, entity, errors);
        }

        Assert.True(errors.Count == 0, $"{errors.Count} sai lệch:\n" + string.Join("\n", errors));
    }

    private static void CheckTable(string domain, string table, ErdSpec.Column[] columns, IEntityType entity, List<string> errors)
    {
        if (entity.GetSchema() != domain) errors.Add($"{table}: schema {entity.GetSchema()} ≠ {domain}");
        var store = StoreObjectIdentifier.Table(table, domain);
        var props = entity.GetProperties().ToDictionary(p => p.GetColumnName(store)!);

        errors.AddRange(props.Keys.Except(columns.Select(c => c.Name)).Select(c => $"{table}.{c}: cột thừa"));

        var pk = columns.Where(c => c.Keys.Contains("PK")).Select(c => c.Name).ToArray();
        var actualPk = entity.FindPrimaryKey()?.Properties.Select(p => p.GetColumnName(store)).ToArray() ?? [];
        if (!pk.SequenceEqual(actualPk)) errors.Add($"{table}: PK [{string.Join(",", actualPk)}] ≠ [{string.Join(",", pk)}]");

        var checks = entity.GetCheckConstraints().ToDictionary(c => c.ModelName, c => c.Sql);
        var expectedFkCount = 0;

        foreach (var c in columns)
        {
            var name = $"{table}.{c.Name}";
            if (!props.TryGetValue(c.Name, out var p)) { errors.Add($"{name}: thiếu cột"); continue; }

            var enumValues = ErdSpec.EnumValues(c);
            var expectedType = ExpectedStoreType(c, enumValues is not null);
            var actualType = p.GetColumnType(store);
            if (expectedType is null ? !Regex.IsMatch(actualType, @"^nvarchar\(\d+\)$") : actualType != expectedType)
                errors.Add($"{name}: kiểu {actualType} ≠ {expectedType ?? "nvarchar(n)"}");

            var nullable = ErdSpec.IsNullable(table, c);
            if (p.IsColumnNullable(store) != nullable) errors.Add($"{name}: nullable = {p.IsColumnNullable(store)}, cần {nullable}");

            if (c.Type == "rowversion" && !(p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate))
                errors.Add($"{name}: chưa là rowversion concurrency token");

            if (enumValues is not null)
            {
                var ck = $"CK_{table}_{c.Name}";
                var allowed = enumValues.Split(", ").Order();
                if (!checks.TryGetValue(ck, out var sql)) errors.Add($"{name}: thiếu {ck}");
                else if (!Regex.Matches(sql!, @"N'(\w+)'").Select(m => m.Groups[1].Value).Order().SequenceEqual(allowed))
                    errors.Add($"{name}: {ck} = {sql} không khớp '{enumValues}'");
            }

            if (c.Keys.Contains("UK"))
            {
                var filter = nullable ? $"[{c.Name}] IS NOT NULL" : null;
                if (!entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0] == p && i.GetFilter() == filter))
                    errors.Add($"{name}: thiếu unique index{(filter is null ? "" : $" filter {filter}")}");
            }

            if (c.Keys.Contains("FK")) CheckFk(domain, table, c, p, entity, errors, ref expectedFkCount);
        }

        if (entity.GetForeignKeys().Count() != expectedFkCount)
            errors.Add($"{table}: có {entity.GetForeignKeys().Count()} FK, cần {expectedFkCount}");

        foreach (var spec in ErdSpec.CompositeIndexes.Where(s => s.Table == table))
        {
            var match = entity.GetIndexes().Any(i =>
                i.Properties.Select(x => x.GetColumnName(store)).SequenceEqual(spec.Columns) && i.IsUnique == spec.Unique
                && i.GetFilter() == spec.Filter && (spec.Name is null || i.GetDatabaseName(store) == spec.Name));
            if (!match) errors.Add($"{table}: thiếu index ({string.Join(", ", spec.Columns)}) unique={spec.Unique} filter={spec.Filter} name={spec.Name}");
        }

        foreach (var (_, ck) in ErdSpec.ExtraChecks.Where(x => x.Table == table))
            if (!checks.ContainsKey(ck)) errors.Add($"{table}: thiếu {ck}");
    }

    private static void CheckFk(string domain, string table, ErdSpec.Column c, IProperty p, IEntityType entity,
        List<string> errors, ref int expectedFkCount)
    {
        var name = $"{table}.{c.Name}";
        var target = ErdSpec.FkTarget(table, c.Name);
        if (!ErdSpec.Entities.TryGetValue(target, out var principal)) { errors.Add($"{name}: không xác định bảng đích {target}"); return; }
        var fk = entity.GetForeignKeys().SingleOrDefault(f => f.Properties.Count == 1 && f.Properties[0] == p);

        if (principal.Domain == domain)
        {
            expectedFkCount++;
            if (fk is null) { errors.Add($"{name}: thiếu FK tới {target}"); return; }
            if (fk.PrincipalEntityType.GetTableName() != target) errors.Add($"{name}: FK tới {fk.PrincipalEntityType.GetTableName()} ≠ {target}");
            if (fk.DeleteBehavior != DeleteBehavior.Restrict) errors.Add($"{name}: OnDelete {fk.DeleteBehavior} ≠ Restrict");
            if (fk.DependentToPrincipal is null) errors.Add($"{name}: thiếu navigation tới {target}");
            return;
        }

        if (fk is not null) errors.Add($"{name}: FK chéo schema tới {principal.Domain}.{target} (MOD-01 cấm)");
        if (!entity.GetIndexes().Any(i => i.Properties[0] == p && i.GetFilter() is null)
            && entity.FindPrimaryKey()!.Properties[0] != p)
            errors.Add($"{name}: thiếu index cho tham chiếu {principal.Domain}.{target}");
    }

    /// <summary>null nghĩa là nvarchar(n) với n cụ thể.</summary>
    private static string? ExpectedStoreType(ErdSpec.Column c, bool isEnum) => c.Type switch
    {
        "guid" => "uniqueidentifier",
        "datetime" => "datetime2",
        "date" => "date",
        "int" => "int",
        "bigint" => "bigint",
        "bool" => "bit",
        "rowversion" => "rowversion",
        "decimal" => ErdSpec.VitalDecimals.Contains(c.Name) ? "decimal(5,2)" : "decimal(18,2)",
        "string" when isEnum => "nvarchar(32)",
        "string" when ErdSpec.IsLongText(c.Name) => "nvarchar(max)",
        "string" => null,
        _ => $"(kiểu ERD lạ: {c.Type})",
    };
}
