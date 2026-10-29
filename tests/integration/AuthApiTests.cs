using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SmartHealthcare.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthApiTests(ApiFactory api)
{
    [Fact]
    public async Task Register_then_login_by_email_phone_or_username_and_read_me()
    {
        var (userName, email, phone) = ApiFactory.NewIdentity();
        var client = api.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { userName, email = email.ToUpperInvariant(), phoneNumber = "+84 " + phone[1..], password = "MatKhau123" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        foreach (var login in new[] { email, phone, userName })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password = "MatKhau123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Patient", body.GetProperty("role").GetString());
            Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());

            using var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
            var account = await (await client.SendAsync(me)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(email, account.GetProperty("email").GetString());
            Assert.Equal(phone, account.GetProperty("phoneNumber").GetString());
            Assert.False(account.GetProperty("emailConfirmed").GetBoolean());
        }
    }

    [Fact]
    public async Task Email_or_phone_already_used_is_conflict()
    {
        var (userName, email, phone) = ApiFactory.NewIdentity();
        var (otherName, otherEmail, otherPhone) = ApiFactory.NewIdentity();
        var client = api.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", new { userName, email, phoneNumber = phone, password = "MatKhau123" })).EnsureSuccessStatusCode();

        var sameEmail = await client.PostAsJsonAsync("/api/auth/register", new { userName = otherName, email, phoneNumber = otherPhone, password = "MatKhau123" });
        var samePhone = await client.PostAsJsonAsync("/api/auth/register", new { userName = otherName, email = otherEmail, phoneNumber = phone, password = "MatKhau123" });

        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, samePhone.StatusCode);
        Assert.Equal("account_already_exists", (await sameEmail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_registrations_with_same_email_create_one_account()
    {
        var (_, email, _) = ApiFactory.NewIdentity();
        var attempts = Enumerable.Range(0, 8).Select(_ =>
        {
            var (userName, _, phone) = ApiFactory.NewIdentity();
            return api.CreateClient().PostAsJsonAsync("/api/auth/register", new { userName, email, phoneNumber = phone, password = "MatKhau123" });
        });

        var statuses = (await Task.WhenAll(attempts)).Select(r => r.StatusCode).ToList();

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.All(statuses.Where(s => s != HttpStatusCode.Created), s => Assert.Equal(HttpStatusCode.Conflict, s));
        Assert.Equal(1, await api.ScalarAsync("SELECT COUNT(*) FROM [dbo].[AppUser] WHERE Email = @e", new SqlParameter("@e", email)));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_account_get_the_same_answer()
    {
        var (userName, email, phone) = ApiFactory.NewIdentity();
        var client = api.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", new { userName, email, phoneNumber = phone, password = "MatKhau123" })).EnsureSuccessStatusCode();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { login = email, password = "SaiMatKhau9" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { login = "khong.ton.tai@example.test", password = "SaiMatKhau9" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("code").GetString(), b.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Invalid_registration_reports_each_field()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { userName = "a", email = "not-an-email", phoneNumber = "123", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "userName", "email", "phoneNumber", "password" })
            Assert.True(errors.TryGetProperty(field, out _), $"thiếu lỗi cho {field}");
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/patients")]
    public async Task Protected_endpoints_require_a_token(string path)
    {
        var response = await api.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Staff_account_logs_in_with_staff_role_and_cannot_use_patient_self_service()
    {
        var (userName, email, phone) = ApiFactory.NewIdentity();
        var client = api.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { userName, email, phoneNumber = phone, password = "MatKhau123" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        await api.ExecuteSqlAsync(
            "INSERT INTO [dbo].[Staff] (Id, AppUserId, StaffCode, FullName, StaffType, IsActive, CreatedAtUtc) VALUES (NEWID(), {0}, {1}, N'Bác sĩ thử', 'Doctor', 1, SYSUTCDATETIME())",
            userId, "BS" + phone[^6..]);

        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { login = userName, password = "MatKhau123" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Doctor", login.GetProperty("role").GetString());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/patients")).StatusCode);
    }
}
