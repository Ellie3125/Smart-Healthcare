using Billing.Contracts;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Notifications.Application.Exceptions;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(BillingPaymentSerialCollection))]
public sealed class BillingStatusReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(50, 100, BookingPaymentStatus.Unknown)]
    [InlineData(0, 0, BookingPaymentStatus.Paid)]
    public async Task Paid_requires_the_expected_amount_and_zero_fee_still_requires_a_verified_transaction(int paid, int expected, BookingPaymentStatus state)
    {
        await using var db = await TestDatabase.OpenAsync();
        var booking = Guid.NewGuid();
        await SeedAsync(db, booking, "Succeeded", true, null);
        await db.QueryAsync($"UPDATE billing.PaymentIntent SET TotalExpectedAmount={expected} WHERE BookingAttemptId='{booking}'");
        await db.QueryAsync($"UPDATE t SET TransactionAmount={paid} FROM billing.PaymentTransaction t JOIN billing.PaymentIntent p ON p.Id=t.PaymentIntentId WHERE p.BookingAttemptId='{booking}'");
        await using var services = Services(db);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(state, (await scope.ServiceProvider.GetRequiredService<IBillingStatusReader>().GetBookingPaymentStatusAsync(booking, Ct)).Status);
    }

    [Fact]
    public async Task Monitor_resolves_real_billing_and_coordination_sources_and_removes_unavailable_flags()
    {
        await using var db = await TestDatabase.OpenAsync();
        var refund = await SeedAsync(db, Guid.NewGuid(), "Succeeded", true, "Pending");
        var encounter = await db.InsertAsync("clinical.Encounter", new { Status = "WaitingCLS" });
        var ticket = await db.InsertAsync("scheduling.QueueTicket", new { EncounterId = encounter, TicketType = "ReturnConsultation",
            ServiceOrderId = Guid.NewGuid(), QueueStatus = "Waiting" });
        await using var services = Services(db, monitor: true);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IWorkflowExceptionMonitor>().GetAsync(100, Ct);
        Assert.Empty(result.UnavailableSources);
        Assert.Contains(result.Items, x => x.Id == refund && x.Source == "Billing" && x.Kind == "PaidUnallocated" && !x.CanRetryOutbox);
        Assert.Contains(result.Items, x => x.Id == ticket && x.Source == "Scheduling" && x.Kind == "OriginalDoctorInactive");
    }

    [Fact]
    public async Task Reader_queries_support_existing_pk_and_booking_attempt_index_seeks()
    {
        await using var db = await TestDatabase.OpenAsync();
        var capture = new QueryCapture();
        await using var services = Services(db, capture: capture);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBillingStatusReader>();
        await reader.GetPaidUnallocatedAsync(Guid.Empty, 100, Ct);
        await reader.GetBookingPaymentStatusAsync(Guid.NewGuid(), Ct);
        Assert.Equal(2, capture.Queries.Count);
        await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString());
        await connection.OpenAsync(Ct);
        await using (var enable = new SqlCommand("SET SHOWPLAN_XML ON", connection)) await enable.ExecuteNonQueryAsync(Ct);
        foreach (var (sql, parameters) in capture.Queries)
        {
            var isRefunds = sql.Contains("FROM billing.RefundRequest r", StringComparison.Ordinal);
            var hint = isRefunds ? "TABLE HINT(r, FORCESEEK)" : "TABLE HINT([p], INDEX([IX_PaymentIntent_BookingAttemptId]), FORCESEEK)";
            var declarations = string.Join("\n", parameters.Select(p => p.Value switch
            {
                Guid id => $"DECLARE @{p.ParameterName.TrimStart('@')} uniqueidentifier = '{id:D}';",
                int value => $"DECLARE @{p.ParameterName.TrimStart('@')} int = {value};",
                _ => throw new InvalidOperationException("Unexpected reader query parameter.")
            }));
            await using var plan = new SqlCommand(declarations + "\n" + sql + " OPTION (" + hint + ")", connection);
            var xml = (string)(await plan.ExecuteScalarAsync(Ct))!;
            Assert.Contains(isRefunds ? "PK_RefundRequest" : "IX_PaymentIntent_BookingAttemptId", xml);
            Assert.Contains("SeekPredicates", xml);
        }
    }

    [Fact]
    public async Task Partial_settled_refund_keeps_remaining_amount_paid_and_cls_payments_do_not_count_as_exam_fees()
    {
        await using var db = await TestDatabase.OpenAsync();
        var booking = Guid.NewGuid();
        await SeedAsync(db, booking, "Succeeded", true, "Settled", refundAmount: 50m);
        var clsBooking = Guid.NewGuid();
        var intent = await db.InsertAsync("billing.PaymentIntent", new { BookingAttemptId = clsBooking, PaymentType = "CLSOrderBatch", Status = "Succeeded", Provider = "VNPAY" });
        await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = intent, PaymentStatus = "Success", TransactionAmount = 100m, VerifiedAtUtc = DateTime.UtcNow });
        await using var services = Services(db);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBillingStatusReader>();
        Assert.Equal(BookingPaymentStatus.Paid, (await reader.GetBookingPaymentStatusAsync(booking, Ct)).Status);
        Assert.Equal(BookingPaymentStatus.Unknown, (await reader.GetBookingPaymentStatusAsync(clsBooking, Ct)).Status);
    }

    [Theory]
    [InlineData("Pending", false, null, BookingPaymentStatus.Pending)]
    [InlineData("Processing", false, null, BookingPaymentStatus.Pending)]
    [InlineData("Failed", false, null, BookingPaymentStatus.Failed)]
    [InlineData("Expired", false, null, BookingPaymentStatus.Failed)]
    [InlineData("Cancelled", false, null, BookingPaymentStatus.Failed)]
    [InlineData("Unknown", false, null, BookingPaymentStatus.Unknown)]
    [InlineData("Succeeded", false, null, BookingPaymentStatus.Unknown)]
    [InlineData("Unknown", true, null, BookingPaymentStatus.Paid)]
    [InlineData("Failed", true, null, BookingPaymentStatus.Paid)]
    [InlineData("Succeeded", true, "Pending", BookingPaymentStatus.RefundPending)]
    [InlineData("Succeeded", true, "Approved", BookingPaymentStatus.RefundPending)]
    [InlineData("Succeeded", true, "Failed", BookingPaymentStatus.RefundPending)]
    [InlineData("Succeeded", true, "Settled", BookingPaymentStatus.Refunded)]
    public async Task Booking_status_uses_verified_payments_and_refunds_without_writing(string intentStatus, bool paid, string? refundStatus, BookingPaymentStatus expected)
    {
        await using var db = await TestDatabase.OpenAsync();
        var booking = Guid.NewGuid();
        await SeedAsync(db, booking, intentStatus, paid, refundStatus);
        await using var services = Services(db);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBillingStatusReader>();
        Assert.Equal(expected, (await reader.GetBookingPaymentStatusAsync(booking, Ct)).Status);
        Assert.Equal(BookingPaymentStatus.Unknown, (await reader.GetBookingPaymentStatusAsync(Guid.NewGuid(), Ct)).Status);
        Assert.False(scope.ServiceProvider.GetRequiredService<BillingDbContext>().ChangeTracker.HasChanges());
        Assert.Equal(intentStatus, Assert.Single(await db.QueryAsync($"SELECT Status FROM billing.PaymentIntent WHERE BookingAttemptId='{booking}'")));
    }

    [Fact]
    public async Task A_kept_successful_payment_remains_paid_while_a_duplicate_is_refunded()
    {
        await using var db = await TestDatabase.OpenAsync();
        var booking = Guid.NewGuid();
        await SeedAsync(db, booking, "Succeeded", true, null);
        await SeedAsync(db, booking, "Succeeded", true, "Settled");
        await SeedAsync(db, booking, "Failed", false, null);
        await using var services = Services(db);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(BookingPaymentStatus.Paid, (await scope.ServiceProvider.GetRequiredService<IBillingStatusReader>().GetBookingPaymentStatusAsync(booking, Ct)).Status);
    }

    [Fact]
    public async Task Paid_unallocated_keyset_returns_only_unsettled_requests_and_a_real_next_cursor()
    {
        await using var db = await TestDatabase.OpenAsync();
        var first = await SeedAsync(db, Guid.NewGuid(), "Succeeded", true, "Pending");
        var second = await SeedAsync(db, Guid.NewGuid(), "Succeeded", true, "Approved");
        var settled = await SeedAsync(db, Guid.NewGuid(), "Succeeded", true, "Settled");
        await using var services = Services(db);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBillingStatusReader>();
        var seen = new HashSet<Guid>();
        Guid cursor = Guid.Empty;
        do
        {
            var page = await reader.GetPaidUnallocatedAsync(cursor, 1, Ct);
            foreach (var item in page.Items) Assert.True(seen.Add(item.RefundRequestId));
            if (!page.HasMore) break;
            Assert.NotNull(page.NextCursor);
            Assert.NotEqual(cursor, page.NextCursor.Value);
            cursor = page.NextCursor.Value;
        } while (true);
        Assert.Contains(first, seen);
        Assert.Contains(second, seen);
        Assert.DoesNotContain(settled, seen);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetPaidUnallocatedAsync(Guid.Empty, 0, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetPaidUnallocatedAsync(Guid.Empty, 101, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetBookingPaymentStatusAsync(Guid.Empty, Ct));
    }

    private static ServiceProvider Services(TestDatabase db, bool monitor = false, QueryCapture? capture = null)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var services = new ServiceCollection().AddBillingInfrastructure(cs);
        services.AddScoped(_ => db.CreateContext<BillingDbContext>(options => new(new DbContextOptionsBuilder<BillingDbContext>(options)
            .AddInterceptors(capture is null ? [] : [capture]).Options)));
        if (monitor)
        {
            services.AddSharedInfrastructure(cs).AddNotificationsInfrastructure(cs).AddSchedulingInfrastructure(cs).AddClinicalInfrastructure(cs);
            services.AddScoped(_ => db.CreateContext<SharedDbContext>(o => new(o)));
            services.AddScoped(_ => db.CreateContext<WorkflowDbContext>(o => new(o)));
            services.AddScoped(_ => db.CreateContext<SchedulingDbContext>(o => new(o)));
            services.AddScoped(_ => db.CreateContext<ClinicalDbContext>(o => new(o)));
            services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(o => new(o)));
            services.AddScoped<IIdentityPatientsModule, IdentityPatientsModule>();
        }
        return services.BuildServiceProvider();
    }
    private static async Task<Guid> SeedAsync(TestDatabase db, Guid booking, string status, bool paid, string? refund, decimal refundAmount = 100m)
    {
        var intent = await db.InsertAsync("billing.PaymentIntent", new { BookingAttemptId = booking, Status = status,
            PaymentType = "InitialConsultation", Provider = "VNPAY", Currency = "VND", TotalExpectedAmount = 100m });
        var transaction = await db.InsertAsync("billing.PaymentTransaction", new { PaymentIntentId = intent, PaymentStatus = paid ? "Success" : "Pending",
            TransactionAmount = 100m, Currency = "VND", VerifiedAtUtc = paid ? (DateTime?)DateTime.UtcNow : null });
        if (refund is null) return Guid.Empty;
        var list = await db.InsertAsync("billing.PriceList", new { Currency = "VND" });
        var price = await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "ConsultationFee", DoctorId = Guid.NewGuid() });
        var item = await db.InsertAsync("billing.BillingItem", new { PriceListItemId = price, ItemType = "ConsultationFee", Status = "Cancelled",
            UnitPrice = 100m, Quantity = 1, TotalAmount = 100m, PaidAmount = 100m });
        var request = await db.InsertAsync("billing.RefundRequest", new { BillingItemId = item, OriginalPaymentTransactionId = transaction,
            RefundReason = "PaidUnallocated", PolicyRuleApplied = "CustomApproval", Status = refund, RequestedAmount = refundAmount });
        if (refund == "Settled") await db.InsertAsync("billing.RefundTransaction", new { RefundRequestId = request, OriginalPaymentTransactionId = transaction,
            RefundedAmount = refundAmount, Currency = "VND", Status = "Success" });
        return request;
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<(string Sql, SqlParameter[] Parameters)> Queries { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries.Add((command.CommandText, command.Parameters.Cast<SqlParameter>().Select(x => (SqlParameter)((ICloneable)x).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
