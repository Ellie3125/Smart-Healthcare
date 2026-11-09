using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SqlServerTests;

/// <summary>
/// SqlException giả với mã lỗi chọn trước. SqlClient không có constructor public nên dựng bằng reflection
/// (SqlError, SqlErrorCollection, SqlException.CreateException là internal).
/// </summary>
internal static class FakeSqlException
{
    private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;

    public static SqlException Create(int number)
    {
        var errorCtor = typeof(SqlError).GetConstructors(Internal)
            .Where(c => c.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(int))
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = errorCtor.GetParameters()
            .Select((p, i) => i == 0 ? number : p.HasDefaultValue ? p.DefaultValue : Default(p.ParameterType))
            .ToArray();
        var error = (SqlError)errorCtor.Invoke(args);

        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", Internal)!.Invoke(errors, [error]);

        var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [errors, "16.0"])!;
    }

    private static object? Default(Type type) => type == typeof(string) ? "" : type.IsValueType ? Activator.CreateInstance(type) : null;
}
