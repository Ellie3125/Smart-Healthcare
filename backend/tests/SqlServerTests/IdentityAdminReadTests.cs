using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IdentityPatients.Api;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

public sealed class IdentityAdminReadTests
{
    private const string Audit = "/api/identity/admin/audit-logs";
    private const string Matrix = "/api/identity/admin/permissions-matrix";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_reads_require_staff_admin()
    {
        await using var f = await ReadHost.CreateAsync();
        foreach (var route in new[] { Audit, Matrix })
        {
            using var anonymous = f.Client(authenticate: false);
            using var denied = await anonymous.GetAsync(route, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            foreach (var (role, channel) in new[] { (Roles.Admin, Channels.Patient), (Roles.Doctor, Channels.Staff),
                (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff), (Roles.Patient, Channels.Patient) })
            {
                using var client = f.Client(role, channel);
                using var response = await client.GetAsync(route, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Matrix_uses_real_composed_endpoint_policies_and_current_role_channels()
    {
        await using var f = await ReadHost.CreateAsync(); using var client = f.Client();
        var rows = await client.GetFromJsonAsync<JsonElement>(Matrix, Ct);
        Assert.Equal(Policies.RoleOf.Values.Distinct().Order(), rows.EnumerateArray().Select(r => r.GetProperty("role").GetString()).Order());
        foreach (var row in rows.EnumerateArray())
        {
            var role = row.GetProperty("role").GetString()!;
            var caps = row.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).ToArray();
            Assert.Equal(role == Roles.Doctor, caps.Contains("GET /test/doctor"));
            Assert.Equal(role == Roles.Nurse, caps.Contains("GET /test/explicit"));
            Assert.Equal(role is Roles.Admin or Roles.Receptionist, caps.Contains("POST /test/counter"));
            Assert.DoesNotContain("GET /test/anonymous", caps);
            Assert.DoesNotContain("GET /test/impossible", caps);
            Assert.Contains(Policies.ChannelOf(role), row.GetProperty("restrictedNote").GetString());
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=1001")]
    [InlineData("?page=bad")]
    [InlineData("?page=1&page=2")]
    [InlineData("?keyword=a&keyword=b")]
    public async Task Invalid_query_is_400(string query)
    {
        await using var f = await ReadHost.CreateAsync(); using var client = f.Client();
        using var response = await client.GetAsync(Audit + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var longKeyword = await client.GetAsync(Audit + "?keyword=" + new string('x', 101), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, longKeyword.StatusCode);
    }

    [Fact]
    public async Task Audit_pages_have_stable_SQL_guid_ties_keyword_filter_and_hasMore()
    {
        await using var f = await ReadHost.CreateAsync();
        var keyword = "AuditTest" + Guid.NewGuid().ToString("N");
        var stamp = new DateTime(2085, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 51; i++) await f.LogAsync(keyword, stamp);
        await f.LogAsync("Unrelated", stamp.AddMinutes(1));
        var expected = await f.Db.QueryAsync($"SELECT CONVERT(nvarchar(36),Id) FROM shared.AuditLog WHERE TableName='{keyword}' ORDER BY TimestampUtc DESC,Id DESC");
        using var client = f.Client();
        var first = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=" + keyword, Ct);
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal(expected.Take(50).Select(Guid.Parse), first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        var second = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=" + keyword + "&page=2", Ct);
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Equal(expected.Skip(50).Select(Guid.Parse), second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        var empty = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=" + keyword + "missing", Ct);
        Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.False(empty.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task Audit_masks_legacy_payloads_and_batch_resolves_current_actor_directory()
    {
        await using var f = await ReadHost.CreateAsync(); var tag = "AuditTest" + Guid.NewGuid().ToString("N");
        var actor = await f.Db.InsertAsync("identity.AppUser", new { UserName = tag, PasswordHash = "not-a-real-password", IsActive = true });
        await f.Db.InsertAsync("identity.Staff", new { AppUserId = actor, FullName = "Test Admin", StaffCode = tag, StaffType = "Admin", IsActive = true });
        var payload = """{"NationalId":"012345678901","child":[{"PhoneNumber":"0901234567","AccessToken":"private","PasswordHash":"private"}],"unchanged":42}""";
        await f.LogAsync(tag, DateTime.UtcNow, actor, payload, payload);
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=" + tag, Ct);
        var row = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Test Admin", row.GetProperty("actorName").GetString()); Assert.Equal(Roles.Admin, row.GetProperty("actorRole").GetString());
        Assert.Equal("UPDATE", row.GetProperty("action").GetString());
        foreach (var name in new[] { "beforeJson", "afterJson" })
        {
            var json = row.GetProperty(name); Assert.Equal("*********901", json.GetProperty("NationalId").GetString());
            var child = json.GetProperty("child")[0]; Assert.Equal("*******567", child.GetProperty("PhoneNumber").GetString());
            Assert.DoesNotContain("private", json.GetRawText()); Assert.Equal(42, json.GetProperty("unchanged").GetInt32());
        }
        Assert.True(row.GetProperty("occurredAt").GetString()!.EndsWith('Z'));
    }

    [Fact]
    public async Task Audit_omits_medical_and_invalid_or_nonobject_payloads_without_failing()
    {
        await using var f = await ReadHost.CreateAsync(); var tag = "AuditTest" + Guid.NewGuid().ToString("N");
        await f.LogAsync("clinical." + tag, DateTime.UtcNow, null, "{\"Diagnosis\":\"private\"}", "{\"NationalId\":\"1234\"}");
        await f.LogAsync("documents." + tag, DateTime.UtcNow, Guid.NewGuid(), "{}", "{}");
        await f.LogAsync(tag, DateTime.UtcNow, null, "invalid legacy json", "[1,2]");
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=" + tag, Ct);
        Assert.Equal(3, result.GetProperty("items").GetArrayLength());
        foreach (var row in result.GetProperty("items").EnumerateArray())
        { Assert.Equal(JsonValueKind.Null, row.GetProperty("beforeJson").ValueKind); Assert.Equal(JsonValueKind.Null, row.GetProperty("afterJson").ValueKind); }
    }

    [Fact]
    public async Task Keyword_searches_existing_metadata_and_unknown_actor_has_no_invented_role()
    {
        await using var f = await ReadHost.CreateAsync(); var tag = "AuditTest" + Guid.NewGuid().ToString("N");
        var missingActor = Guid.NewGuid();
        await f.Db.InsertAsync("shared.AuditLog", new { TableName = "test", TimestampUtc = DateTime.UtcNow, PerformedByUserId = missingActor,
            RecordId = Guid.NewGuid(), ChangedColumns = tag, ActionType = "UPDATE", OldValuesJson = "{}" });
        await f.Db.InsertAsync("shared.AuditLog", new { TableName = "test", TimestampUtc = DateTime.UtcNow,
            RecordId = Guid.NewGuid(), ChangedColumns = "Value", ChangeReason = tag, ActionType = "UPDATE" });
        await f.LogAsync("not metadata", DateTime.UtcNow, null, "{\"value\":\"" + tag + "\"}", null);
        using var client = f.Client(); var result = await client.GetFromJsonAsync<JsonElement>(Audit + "?keyword=%20" + tag + "%20", Ct);
        var rows = result.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(2, rows.Length);
        Assert.Contains(rows, r => r.GetProperty("actorName").GetString() == "System");
        var missing = Assert.Single(rows.Where(r => r.GetProperty("actorName").GetString() == missingActor.ToString()));
        Assert.Equal("", missing.GetProperty("actorRole").GetString());
    }

    private sealed class ReadHost(WebApplication app, TestDatabase db) : IAsyncDisposable
    {
        public TestDatabase Db => db;
        public static async Task<ReadHost> CreateAsync()
        {
            var db = await TestDatabase.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "AdminRead.Tests";
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, TestDatabase.RequiredConnectionString())
                .AddSharedInfrastructure(TestDatabase.RequiredConnectionString()).AddIdentityPatientsApi();
            builder.Services.AddScoped(_ => db.CreateContext<SharedDbContext>(o => new SharedDbContext(o)));
            builder.Services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(o => new IdentityPatientsDbContext(o)));
            var app = builder.Build(); app.MapIdentityPatientsEndpoints();
            app.MapGet("/test/doctor", () => "test").RequireAuthorization(Policies.DoctorOnly);
            app.MapGet("/test/explicit", () => "test").RequireAuthorization(new AuthorizationPolicyBuilder()
                .RequireRole(Roles.Nurse).RequireClaim(AppClaims.Channel, Channels.Staff).Build());
            app.MapPost("/test/counter", () => "test").RequireAuthorization(Policies.ReceptionistOrAdmin);
            app.MapGet("/test/anonymous", () => "test").AllowAnonymous();
            app.MapGet("/test/impossible", () => "test").RequireAuthorization(Policies.AdminOnly, Policies.PatientOnly);
            await app.StartAsync(Ct); return new(app, db);
        }
        public HttpClient Client(string role = Roles.Admin, string channel = Channels.Staff, bool authenticate = true)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            if (authenticate)
            {
                var token = new JwtSecurityToken("AdminRead.Tests", "AdminRead.Tests", [new("sub", Guid.NewGuid().ToString()), new("role", role), new(AppClaims.Channel, channel)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }
        public Task<Guid> LogAsync(string table, DateTime time, Guid? actor = null, string? before = "{}", string? after = "{}") =>
            db.InsertAsync("shared.AuditLog", new { TableName = table, TimestampUtc = time, PerformedByUserId = actor,
                RecordId = Guid.NewGuid(), ChangedColumns = "Test", ActionType = "UPDATE", OldValuesJson = before, NewValuesJson = after });
        public async ValueTask DisposeAsync() { await app.StopAsync(Ct); await app.DisposeAsync(); await db.DisposeAsync(); }
    }
}
