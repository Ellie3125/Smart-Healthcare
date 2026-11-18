using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using MedicationCatalog.Api;
using MedicationCatalog.Infrastructure;
using MedicationCatalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class MedicationCatalogEndpointTests
{
    private const string Route = "/api/medication-catalog/staff/drugs";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lookup_requires_doctor_in_staff_channel()
    {
        await using var db = await TestDatabase.OpenAsync();
        await using var factory = new DrugFactory(db);
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync(Route, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Doctor, Channels.Patient), (Roles.Patient, Channels.Patient),
                     (Roles.Nurse, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            factory.Authorize(client, role, channel);
            using var denied = await client.GetAsync(Route, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        factory.Authorize(client);
        using var allowed = await client.GetAsync(Route, Ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task SQL_lookup_filters_active_rows_limits_twenty_and_matches_code_name_or_ingredient_literally()
    {
        await using var db = await TestDatabase.OpenAsync();
        var prefix = Guid.NewGuid().ToString("N");
        for (var n = 21; n >= 0; n--)
            await db.InsertAsync("medication.MedicationCatalog", new { MedicationCode = prefix + n.ToString("D2"),
                MedicationName = prefix + "-" + n.ToString("D2"), ActiveIngredient = "ingredient-" + prefix,
                DosageForm = "Tablet", Unit = "Vien", Strength = "10 mg", IsActive = n != 21 });
        await using var factory = new DrugFactory(db);
        using var client = factory.CreateClient();
        factory.Authorize(client);
        foreach (var q in new[] { prefix, "ingredient-" + prefix, "  " + prefix + "  " })
        {
            using var response = await client.GetAsync(Route + "?q=" + Uri.EscapeDataString(q), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(20, items.GetArrayLength());
            Assert.Equal(prefix + "-00", items[0].GetProperty("name").GetString());
            Assert.Equal("Vien", items[0].GetProperty("unit").GetString());
            Assert.Equal("10 mg", items[0].GetProperty("strength").GetString());
            Assert.Equal("", items[0].GetProperty("defaultDosage").GetString());
            Assert.DoesNotContain(items.EnumerateArray(), x => x.GetProperty("name").GetString() == prefix + "-21");
        }
        foreach (var q in new[] { prefix + "21", "%' OR 1=1 --" })
        {
            using var response = await client.GetAsync(Route + "?q=" + Uri.EscapeDataString(q), Ct);
            Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray());
        }
    }

    [Theory]
    [InlineData(101, false)]
    [InlineData(1, true)]
    public async Task Invalid_query_is_400(int length, bool control)
    {
        await using var db = await TestDatabase.OpenAsync();
        await using var factory = new DrugFactory(db);
        using var client = factory.CreateClient();
        factory.Authorize(client);
        using var response = await client.GetAsync(Route + "?q=" + Uri.EscapeDataString(control ? "\0" : new string('x', length)), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidInput", await response.Content.ReadAsStringAsync(Ct));
    }

    private sealed class DrugFactory(TestDatabase database) : WebApplicationFactory<MedicationCatalogEndpointTests>
    {
        private readonly string _key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        protected override IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHostDefaults(web => web.UseEnvironment("Production").ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Jwt:SigningKey"] = _key, ["Jwt:Issuer"] = "Drug.Tests", ["Jwt:Audience"] = "Drug.Tests" }).Build();
                services.AddIdentityPatientsInfrastructure(configuration, TestDatabase.RequiredConnectionString())
                    .AddMedicationCatalogInfrastructure(TestDatabase.RequiredConnectionString()).AddMedicationCatalogApi();
                services.AddScoped(_ => database.CreateContext<MedicationDbContext>(options => new(options)));
            }).Configure(app =>
            {
                app.UseRouting().UseAuthentication().UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapMedicationCatalogEndpoints());
            }));

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseContentRoot(AppContext.BaseDirectory);

        public void Authorize(HttpClient client, string role = Roles.Doctor, string channel = Channels.Staff)
        {
            var token = new JwtSecurityToken("Drug.Tests", "Drug.Tests",
                [new("sub", Guid.NewGuid().ToString()), new("role", role), new(AppClaims.Channel, channel)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
    }
}
