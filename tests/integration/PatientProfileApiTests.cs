using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SmartHealthcare.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PatientProfileApiTests(ApiFactory api)
{
    private static int _cccd;

    private static string NewNationalId() => $"0010{Interlocked.Increment(ref _cccd):D8}";

    private static object Profile(string relationship, string? nationalId = null, string fullName = "Nguyễn Văn An") =>
        new { relationship, fullName, dateOfBirth = "1990-05-01", gender = "Male", nationalId, phoneNumber = "0901234567", address = "Quận 1, TP.HCM" };

    private static async Task<JsonElement> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/patients", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Created_profiles_are_listed_with_masked_national_id()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        var nationalId = NewNationalId();

        var self = await CreateAsync(client, Profile("Self", nationalId));
        await CreateAsync(client, Profile("Child", fullName: "Nguyễn Bảo Châu"));

        Assert.Equal("********" + nationalId[^4..], self.GetProperty("nationalIdMasked").GetString());
        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/patients");
        Assert.Equal(["Self", "Child"], list!.Select(p => p.GetProperty("relationship").GetString()));
        Assert.DoesNotContain(nationalId, await (await client.GetAsync("/api/patients")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Timestamps_are_utc_and_identical_after_reload()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        var created = await CreateAsync(client, Profile("Self"));
        var id = created.GetProperty("id").GetGuid();

        var createdAt = created.GetProperty("createdAtUtc").GetString()!;
        var reloadedAt = (await client.GetFromJsonAsync<JsonElement>($"/api/patients/{id}")).GetProperty("createdAtUtc").GetString()!;
        var accountCreatedAt = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("createdAtUtc").GetString()!;

        Assert.EndsWith("Z", createdAt);
        Assert.EndsWith("Z", accountCreatedAt);
        Assert.Equal(createdAt, reloadedAt);
    }

    [Fact]
    public async Task Account_has_at_most_one_self_profile()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        await CreateAsync(client, Profile("Self"));

        var second = await client.PostAsJsonAsync("/api/patients", Profile("Self"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("self_profile_exists", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_self_profile_requests_create_exactly_one()
    {
        var (client, userId) = await api.RegisterAndLoginAsync();

        var statuses = (await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync("/api/patients", Profile("Self")))))
            .Select(r => r.StatusCode).ToList();

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.All(statuses.Where(s => s != HttpStatusCode.Created), s => Assert.Equal(HttpStatusCode.Conflict, s));
        Assert.Equal(1, await api.ScalarAsync(
            "SELECT COUNT(*) FROM [dbo].[PatientRepresentative] WHERE AppUserId = @u AND Relationship = 'Self'",
            new SqlParameter("@u", userId)));
    }

    [Fact]
    public async Task Another_account_cannot_read_update_or_unlink_the_profile()
    {
        var (owner, _) = await api.RegisterAndLoginAsync();
        var (stranger, _) = await api.RegisterAndLoginAsync();
        var profile = await CreateAsync(owner, Profile("Self"));
        var id = profile.GetProperty("id").GetGuid();
        var version = profile.GetProperty("version").GetString();

        var read = await stranger.GetAsync($"/api/patients/{id}");
        var update = await stranger.PutAsJsonAsync($"/api/patients/{id}",
            new { version, fullName = "Kẻ Lạ", dateOfBirth = "1990-05-01", gender = "Male" });
        var unlink = await stranger.DeleteAsync($"/api/patients/{id}/my-link");
        var missing = await stranger.GetAsync($"/api/patients/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unlink.StatusCode);
        // Hồ sơ của người khác và hồ sơ không tồn tại phải trả cùng một lỗi (chỉ traceId khác nhau).
        var readError = await read.Content.ReadFromJsonAsync<JsonElement>();
        var missingError = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(missingError.GetProperty("code").GetString(), readError.GetProperty("code").GetString());
        Assert.Equal(missingError.GetProperty("detail").GetString(), readError.GetProperty("detail").GetString());
        Assert.Empty((await stranger.GetFromJsonAsync<JsonElement[]>("/api/patients"))!);
        Assert.Equal("Nguyễn Văn An", (await owner.GetFromJsonAsync<JsonElement>($"/api/patients/{id}")).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Update_with_stale_version_is_rejected_without_overwriting()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        var created = await CreateAsync(client, Profile("Self"));
        var id = created.GetProperty("id").GetGuid();
        var staleVersion = created.GetProperty("version").GetString();

        var first = await client.PutAsJsonAsync($"/api/patients/{id}",
            new { version = staleVersion, fullName = "Nguyễn Văn Bình", dateOfBirth = "1990-05-01", gender = "Male", phoneNumber = "0901234567" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var newVersion = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString();
        Assert.NotEqual(staleVersion, newVersion);

        var changedStale = await client.PutAsJsonAsync($"/api/patients/{id}",
            new { version = staleVersion, fullName = "Nguyễn Văn Cường", dateOfBirth = "1990-05-01", gender = "Male" });
        var unchangedStale = await client.PutAsJsonAsync($"/api/patients/{id}",
            new { version = staleVersion, fullName = "Nguyễn Văn Bình", dateOfBirth = "1990-05-01", gender = "Male", phoneNumber = "0901234567", address = "Quận 1, TP.HCM" });

        Assert.Equal(HttpStatusCode.Conflict, changedStale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unchangedStale.StatusCode);
        Assert.Equal("profile_version_conflict", (await changedStale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("Nguyễn Văn Bình", (await client.GetFromJsonAsync<JsonElement>($"/api/patients/{id}")).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task National_id_registered_by_another_account_is_conflict()
    {
        var (first, _) = await api.RegisterAndLoginAsync();
        var (second, _) = await api.RegisterAndLoginAsync();
        var nationalId = NewNationalId();
        await CreateAsync(first, Profile("Self", nationalId));

        var duplicate = await second.PostAsJsonAsync("/api/patients", Profile("Self", nationalId));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("national_id_already_registered", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync("/api/patients", Profile("Self"))).StatusCode);
    }

    [Fact]
    public async Task Unlinked_profile_is_no_longer_accessible_but_is_kept()
    {
        var (client, _) = await api.RegisterAndLoginAsync();
        var id = (await CreateAsync(client, Profile("Parent", fullName: "Lê Thị Hoa"))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/patients/{id}/my-link")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/patients/{id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/patients"))!);
        Assert.Equal(1, await api.ScalarAsync("SELECT COUNT(*) FROM [dbo].[Patient] WHERE Id = @id", new SqlParameter("@id", id)));
    }

    [Fact]
    public async Task Invalid_profile_reports_each_field()
    {
        var (client, _) = await api.RegisterAndLoginAsync();

        var response = await client.PostAsJsonAsync("/api/patients",
            new { relationship = "Friend", fullName = "A", dateOfBirth = "2999-01-01", gender = "X", nationalId = "12ab" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "relationship", "fullName", "dateOfBirth", "gender", "nationalId" })
            Assert.True(errors.TryGetProperty(field, out _), $"thiếu lỗi cho {field}");
    }
}
