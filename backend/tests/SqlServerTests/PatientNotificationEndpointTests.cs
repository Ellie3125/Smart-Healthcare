using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api;
using Notifications.Application.PatientNotifications;
using Xunit;

namespace SqlServerTests;

public sealed class PatientNotificationEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_and_read_require_patient_channel_and_take_user_from_the_token(bool read)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:SigningKey"] = key;
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Notification.Tests";
        builder.Services.AddIdentityPatientsAuth(builder.Configuration).AddNotificationsApi();
        var reader = new Reader(); builder.Services.AddSingleton<IPatientNotificationReader>(reader);
        await using var app = builder.Build(); app.MapNotificationsEndpoints(); await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var patient = Guid.NewGuid(); var id = Guid.NewGuid(); var user = Guid.NewGuid();
        const string root = "/api/notifications/patient/notifications";
        var path = read ? root + $"/{id}/read" : root + $"?patientId={patient}";
        async Task<HttpResponseMessage> Send() => read ? await client.PostAsJsonAsync(path, new { userId = Guid.NewGuid() }, ct) : await client.GetAsync(path, ct);
        using var anonymous = await Send(); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Admin, Channels.Staff), (Roles.Doctor, Channels.Staff), (Roles.RoomAccount, Channels.Staff), (Roles.Patient, Channels.Staff) })
        {
            Authorize(role, channel);
            using var forbidden = await Send(); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        Authorize(Roles.Patient, Channels.Patient);
        using var success = await Send(); Assert.Equal(read ? HttpStatusCode.NoContent : HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(user, reader.User);
        if (read)
        {
            Assert.Equal(id, reader.Id);
            reader.Found = false;
            using var hidden = await Send(); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        else
        {
            Assert.Equal(patient, reader.Id);
            using var missing = await client.GetAsync(root, ct); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            reader.Deny = true;
            using var hidden = await Send(); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        void Authorize(string role, string channel)
        {
            var jwt = new JwtSecurityToken("Notification.Tests", "Notification.Tests", [new("sub", user.ToString()), new("role", role), new(AppClaims.Channel, channel)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
    }
    private sealed class Reader : IPatientNotificationReader
    {
        public Guid User { get; private set; } public Guid Id { get; private set; }
        public bool Found { get; set; } = true; public bool Deny { get; set; }
        public Task<PatientNotificationPage> ListAsync(Guid u, Guid p, bool unread, int page, int size, CancellationToken ct)
        {
            User = u; Id = p;
            if (Deny) throw new PatientNotificationException(PatientNotificationError.NotFound);
            return Task.FromResult(new PatientNotificationPage([], 0, false));
        }
        public Task<bool> MarkReadAsync(Guid u, Guid id, CancellationToken ct) { User = u; Id = id; return Task.FromResult(Found); }
    }
}
