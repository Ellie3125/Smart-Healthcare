using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartHealthcare.Database;
using SmartHealthcare.Identity.Infrastructure.Persistence;
using SmartHealthcare.Patients.Infrastructure.Persistence;

namespace SmartHealthcare.IntegrationTests;

/// <summary>
/// Chạy API thật trên một database SQL Server tạm, tạo schema bằng đúng các migration trong backend/src/Database rồi xóa khi xong.
/// Đặt SMARTHEALTHCARE_TEST_SQLSERVER để trỏ tới server khác; mặc định là localhost, Windows Authentication.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // TrustServerCertificate chỉ dùng cho SQL Server cục bộ có chứng chỉ tự ký khi chạy test trên máy dev.
    private const string DefaultServer = "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
    private static int _sequence;

    public ApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("SMARTHEALTHCARE_TEST_SQLSERVER") ?? DefaultServer;
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = $"SmartHealthcare_Test_{Guid.NewGuid():N}" }.ConnectionString;
    }

    public string ConnectionString { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SmartHealthcare", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-0123456789");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "100000");
    }

    public async Task InitializeAsync()
    {
        await using var schema = CreateSchemaContext();
        await schema.Database.MigrateAsync();
    }

    /// <summary>Context chỉ dùng để áp migration của backend/src/Database lên database tạm.</summary>
    public SchemaDbContext CreateSchemaContext()
    {
        var builder = new DbContextOptionsBuilder<SchemaDbContext>();
        SchemaDbContext.Configure(builder, ConnectionString);
        return new SchemaDbContext(builder.Options);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>Thông tin đăng ký không trùng giữa các test trong cùng lần chạy.</summary>
    public static (string UserName, string Email, string Phone) NewIdentity()
    {
        var n = Interlocked.Increment(ref _sequence);
        return ($"user{n:D5}", $"user{n:D5}@example.test", $"09{n:D8}");
    }

    public async Task<(HttpClient Client, Guid UserId)> RegisterAndLoginAsync()
    {
        var (userName, email, phone) = NewIdentity();
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { userName, email, phoneNumber = phone, password = "MatKhau123" });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { login = email, password = "MatKhau123" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return (client, body.GetProperty("userId").GetGuid());
    }

    public async Task<int> ExecuteSqlAsync(string sql, params object[] args)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PatientsModuleDbContext>().Database.ExecuteSqlRawAsync(sql, args);
    }

    public async Task<int> ScalarAsync(string sql, params SqlParameter[] args)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(args);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
