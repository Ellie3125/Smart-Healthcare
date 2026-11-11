using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure;
using SqlServerTests;

namespace IdentityPatients.Tests;

/// <summary>
/// DI thật trên SmartHealthcare_Test với transaction thật của IUnitOfWork, cho test đồng thời và test rollback.
/// Dữ liệu được commit, nên mọi AppUser/Room/Patient tạo ra phải ghi vào <see cref="Track"/> để dọn khi dispose.
/// </summary>
internal sealed class LiveIdentity : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private readonly HashSet<Guid> _users = [];
    private readonly HashSet<Guid> _rooms = [];
    private readonly HashSet<Guid> _patients = [];

    private LiveIdentity(string connectionString, ServiceProvider root) => (ConnectionString, _root) = (connectionString, root);

    public string ConnectionString { get; }

    public static LiveIdentity Create()
    {
        var connectionString = TestDatabase.RequiredConnectionString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedInfrastructure(connectionString)
            .AddIdentityPatientsInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>();
        return new LiveIdentity(connectionString, services.BuildServiceProvider());
    }

    public AsyncServiceScope Scope() => _root.CreateAsyncScope();

    public void Track(Guid? user = null, Guid? room = null, Guid? patient = null)
    {
        if (user is { } u) _users.Add(u);
        if (room is { } r) _rooms.Add(r);
        if (patient is { } p) _patients.Add(p);
    }

    public async ValueTask DisposeAsync()
    {
        static string In(IEnumerable<Guid> ids) => ids.Any() ? string.Join(",", ids.Select(id => $"'{id}'")) : "NULL";
        var users = In(_users);
        var rooms = In(_rooms);
        var patients = In(_patients);
        var sql = $"""
            DELETE FROM [shared].[AuditLog] WHERE [PerformedByUserId] IN ({users});
            DELETE s FROM [identity].[RoomDeviceSession] s JOIN [identity].[RoomAccount] a ON a.[Id] = s.[RoomAccountId] WHERE a.[AppUserId] IN ({users}) OR a.[RoomId] IN ({rooms});
            DELETE x FROM [identity].[StaffShiftAssignment] x LEFT JOIN [identity].[Staff] s ON s.[Id] = x.[StaffId] WHERE s.[AppUserId] IN ({users}) OR x.[RoomId] IN ({rooms});
            DELETE FROM [identity].[PatientRepresentative] WHERE [AppUserId] IN ({users}) OR [PatientId] IN ({patients});
            DELETE FROM [identity].[Patient] WHERE [Id] IN ({patients});
            DELETE d FROM [identity].[Doctor] d JOIN [identity].[Staff] s ON s.[Id] = d.[StaffId] WHERE s.[AppUserId] IN ({users});
            DELETE FROM [identity].[RoomAccount] WHERE [AppUserId] IN ({users}) OR [RoomId] IN ({rooms});
            DELETE FROM [identity].[Staff] WHERE [AppUserId] IN ({users});
            DELETE FROM [identity].[Room] WHERE [Id] IN ({rooms});
            DELETE FROM [identity].[AppUser] WHERE [Id] IN ({users});
            """;
        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        await _root.DisposeAsync();
    }
}
