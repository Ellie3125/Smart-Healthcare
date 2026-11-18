using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using DocumentsReporting.Api;
using DocumentsReporting.Application;
using DocumentsReporting.Infrastructure;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Api;
using Scheduling.Application;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class AdminReportSummaryTests
{
    private const string Route = "/api/documents/admin/reports/summary";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Summary_requires_admin_in_staff_channel()
    {
        await using var db = await TestDatabase.OpenAsync();
        await using var app = await ReportHost.CreateAsync(db);
        using var anonymous = app.Client(authenticate: false);
        using var noSession = await anonymous.GetAsync(Route + "?dateFrom=2084-01-01&dateTo=2084-01-01", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Admin, Channels.Patient), (Roles.Patient, Channels.Patient),
                     (Roles.Doctor, Channels.Staff), (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            using var client = app.Client(role, channel);
            using var response = await client.GetAsync(Route + "?dateFrom=2084-01-01&dateTo=2084-01-01", Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("?dateFrom=2084-1-01&dateTo=2084-01-01")]
    [InlineData("?dateFrom=2084-01-01&dateTo=invalid")]
    [InlineData("?dateFrom=2084-01-02&dateTo=2084-01-01")]
    [InlineData("?dateFrom=2084-01-01&dateTo=2084-02-01")]
    [InlineData("?dateFrom=0001-01-01&dateTo=0001-01-01")]
    [InlineData("?dateFrom=9999-12-31&dateTo=9999-12-31")]
    public async Task Invalid_or_overlong_interval_is_400(string query)
    {
        await using var db = await TestDatabase.OpenAsync();
        await using var app = await ReportHost.CreateAsync(db);
        using var client = app.Client();
        using var response = await client.GetAsync(Route + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidInput", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Thirty_one_inclusive_days_are_allowed_and_empty_days_are_zero_filled()
    {
        await using var db = await TestDatabase.OpenAsync();
        await using var app = await ReportHost.CreateAsync(db);
        using var client = app.Client();
        using var response = await client.GetAsync(Route + "?dateFrom=2084-03-01&dateTo=2084-03-31", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(31, json.GetProperty("dailySeries").GetArrayLength());
        Assert.Equal(0, json.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(0, json.GetProperty("totalAppointments").GetInt32());
        Assert.Equal("2084-03-01", json.GetProperty("dailySeries")[0].GetProperty("date").GetString());
        Assert.Equal("2084-03-31", json.GetProperty("dailySeries")[30].GetProperty("date").GetString());
    }

    [Fact]
    public async Task SQL_summary_uses_local_day_actual_receipts_and_refunds_and_counts_each_source_once()
    {
        await using var db = await TestDatabase.OpenAsync();
        var start = new DateTime(2084, 1, 1, 17, 0, 0, DateTimeKind.Utc); // 02/01 GMT+7.
        var quota = await db.InsertAsync("scheduling.DoctorDayQuota", new { ServiceDate = start.Date, SessionStatus = "Scheduled" });
        var attempt = await db.InsertAsync("scheduling.BookingAttempt", new { CurrentStep = "DoctorSelection", Status = "Draft" });
        var reservation = await db.InsertAsync("scheduling.Reservation", new { SessionQuotaId = quota, BookingAttemptId = attempt,
            BookingChannel = "Online", Status = "Confirmed" });
        await db.InsertAsync("scheduling.Appointment", new { ReservationId = reservation, SessionQuotaId = quota,
            Status = "Cancelled", ConfirmedAtUtc = start, CancelledAtUtc = start.AddHours(1) });
        await db.InsertAsync("scheduling.QueueTicket", new { IssuedAtUtc = start, TicketType = "InitialConsultation", QueueStatus = "Finished" });
        await db.InsertAsync("scheduling.QueueTicket", new { IssuedAtUtc = start.AddDays(1), TicketType = "CLS", QueueStatus = "Waiting" });
        await db.InsertAsync("clinical.Encounter", new { CheckedInAtUtc = start, Status = "Completed" });
        await db.InsertAsync("clinical.Encounter", new { CheckedInAtUtc = start.AddSeconds(-1), Status = "InProgress" });
        async Task<Guid> Receipt(string provider, string status, DateTime? verified, decimal amount, string currency = "VND")
        {
            var intent = await db.InsertAsync("billing.PaymentIntent", new { Provider = provider, Status = "Succeeded", PaymentType = "InitialConsultation" });
            return await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = intent, PaymentStatus = status,
                VerifiedAtUtc = verified, TransactionAmount = amount, Currency = currency });
        }
        var cash = await Receipt("Counter", "Success", start, 100m);
        await Receipt("VNPAY", "Success", start.AddHours(1), 200m);
        await Receipt("Counter", "Success", start.AddDays(1), 900m);
        await Receipt("Counter", "Success", start.AddSeconds(-1), 800m);
        await Receipt("VNPAY", "Failed", start, 700m);
        await Receipt("VNPAY", "Pending", start, 600m);
        await Receipt("VNPAY", "Unknown", start, 500m);
        await Receipt("Counter", "Success", null, 400m);
        await Receipt("Counter", "Success", start, 300m, "USD");
        var list = await db.InsertAsync("billing.PriceList");
        var price = await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = Guid.NewGuid() });
        var item = await db.InsertAsync("billing.BillingItem", new { PriceListItemId = price, ItemType = "CLSService", Status = "Paid" });
        async Task Refund(string status, DateTime processed, decimal amount)
        {
            var request = await db.InsertAsync("billing.RefundRequest", new { BillingItemId = item, OriginalPaymentTransactionId = cash,
                RefundReason = "SystemError", PolicyRuleApplied = "CustomApproval", Status = "Settled" });
            await db.InsertAsync("billing.RefundTransaction", new { RefundRequestId = request, OriginalPaymentTransactionId = cash,
                Status = status, ProcessedAtUtc = processed, RefundedAmount = amount, Currency = "VND" });
        }
        await Refund("Success", start, 10m);
        await Refund("Failed", start, 30m);
        await Refund("Success", start.AddDays(1), 50m);
        await using var app = await ReportHost.CreateAsync(db);
        using var client = app.Client();
        using var response = await client.GetAsync(Route + "?dateFrom=2084-01-02&dateTo=2084-01-02", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        foreach (var key in new[] { "totalAppointments", "cancelledCount", "checkedInCount", "totalEncounters", "totalTickets" })
            Assert.Equal(1, json.GetProperty(key).GetInt32());
        Assert.Equal(100m, json.GetProperty("cashRevenue").GetDecimal());
        Assert.Equal(200m, json.GetProperty("onlineRevenue").GetDecimal());
        Assert.Equal(300m, json.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(10m, json.GetProperty("totalRefunded").GetDecimal());
        var day = Assert.Single(json.GetProperty("dailySeries").EnumerateArray());
        Assert.Equal(300m, day.GetProperty("revenue").GetDecimal());
        Assert.Equal(1, day.GetProperty("bookings").GetInt32());
    }

    private sealed class ReportHost(WebApplication app) : IAsyncDisposable
    {
        public static async Task<ReportHost> CreateAsync(TestDatabase database)
        {
            var cs = TestDatabase.RequiredConnectionString();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Report.Tests";
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
                .AddSchedulingInfrastructure(cs).AddSchedulingApplication().AddSchedulingApi()
                .AddBillingInfrastructure(cs).AddClinicalInfrastructure(cs)
                .AddDocumentsReportingInfrastructure(cs).AddDocumentsReportingApplication().AddDocumentsReportingApi();
            builder.Services.AddScoped(_ => database.CreateContext<SchedulingDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => database.CreateContext<ClinicalDbContext>(o => new(o)));
            builder.Services.AddScoped(_ => database.CreateContext<BillingDbContext>(o => new(o)));
            var app = builder.Build();
            app.MapDocumentsReportingEndpoints();
            await app.StartAsync(Ct);
            return new(app);
        }

        public HttpClient Client(string role = Roles.Admin, string channel = Channels.Staff, bool authenticate = true)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            if (authenticate)
            {
                var token = new JwtSecurityToken("Report.Tests", "Report.Tests",
                    [new("sub", Guid.NewGuid().ToString()), new("role", role), new(AppClaims.Channel, channel)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }

        public async ValueTask DisposeAsync() { await app.StopAsync(Ct); await app.DisposeAsync(); }
    }
}
