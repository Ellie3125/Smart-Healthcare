using System.Net;
using System.Text;
using System.Text.Json;
using Billing.Application;
using Billing.Infrastructure.Gateway;
using Microsoft.Extensions.Options;
using Xunit;

namespace Billing.Tests;

public class VnpayStatusGatewayTests
{
    private const string Secret = "test-hash-secret-0123456789";
    private const string TmnCode = "TESTCODE";
    private const string Reference = "REF0001";
    private static readonly DateTime Now = new(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Created = new(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static VnpayOptions Settings() => new() { TmnCode = TmnCode, HashSecret = Secret, ReturnUrl = "http://localhost/return" };

    [Fact]
    public async Task Request_is_signed_over_pipe_joined_fields_with_original_create_date()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK, Response(("vnp_ResponseCode", "91"), ("vnp_Message", "Not found"))));
        await Gateway(handler).QueryAsync(new GatewayStatusQuery(Reference, Created, "Truy van PI-1"), Ct);

        var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(handler.Body!)!;
        Assert.Equal(("querydr", "2.1.0", TmnCode, Reference), (sent["vnp_Command"], sent["vnp_Version"], sent["vnp_TmnCode"], sent["vnp_TxnRef"]));
        Assert.Equal("20261116090000", sent["vnp_TransactionDate"]); // vnp_CreateDate của lần tạo liên kết, giờ GMT+7
        Assert.Equal("20261116100000", sent["vnp_CreateDate"]);      // lúc truy vấn
        var data = string.Join('|', sent["vnp_RequestId"], sent["vnp_Version"], sent["vnp_Command"], sent["vnp_TmnCode"], sent["vnp_TxnRef"],
            sent["vnp_TransactionDate"], sent["vnp_CreateDate"], sent["vnp_IpAddr"], sent["vnp_OrderInfo"]);
        Assert.Equal(VnpaySignature.Sign(Secret, data), sent["vnp_SecureHash"]);
        Assert.Equal("https://sandbox.vnpayment.vn/merchant_webapi/api/transaction", handler.Uri!.ToString());
    }

    [Theory]
    [InlineData("00", GatewayPaymentState.Paid)]
    [InlineData("01", GatewayPaymentState.Pending)]
    [InlineData("02", GatewayPaymentState.Failed)]
    [InlineData("04", GatewayPaymentState.NeedsReview)]
    [InlineData("07", GatewayPaymentState.NeedsReview)]
    public void Transaction_status_maps_to_payment_state(string transactionStatus, GatewayPaymentState expected)
    {
        var status = VnpayStatusGateway.Interpret(Response(("vnp_ResponseCode", "00"), ("vnp_TransactionStatus", transactionStatus)), Reference, Settings());

        Assert.Equal(expected, status.State);
        Assert.Equal(transactionStatus, status.GatewayCode);
    }

    [Fact]
    public void Paid_status_carries_vnpay_transaction_number_amount_divided_by_100_and_pay_date()
    {
        var status = VnpayStatusGateway.Interpret(Response(("vnp_ResponseCode", "00"), ("vnp_TransactionStatus", "00")), Reference, Settings());

        Assert.Equal(("14100001", 290_000m), (status.ProviderTransactionId, status.Amount));
        Assert.Equal(new DateTime(2026, 11, 16, 2, 5, 0, DateTimeKind.Utc), status.PaidAtUtc);
    }

    [Fact]
    public void Not_found_is_reported_not_thrown()
    {
        var status = VnpayStatusGateway.Interpret(Response(("vnp_ResponseCode", "91"), ("vnp_Message", "Not found")), Reference, Settings());
        Assert.Equal(GatewayPaymentState.NotFound, status.State);
    }

    [Theory]
    [InlineData("97")]
    [InlineData("99")]
    [InlineData("94")]
    public void Query_errors_are_gateway_exceptions(string responseCode)
    {
        Assert.Throws<PaymentGatewayException>(() =>
            VnpayStatusGateway.Interpret(Response(("vnp_ResponseCode", responseCode)), Reference, Settings()));
    }

    [Fact]
    public void Tampered_signature_other_merchant_or_other_reference_is_rejected()
    {
        var tampered = Response(("vnp_ResponseCode", "00"), ("vnp_TransactionStatus", "00")).Replace("29000000", "1");
        Assert.Throws<PaymentGatewayException>(() => VnpayStatusGateway.Interpret(tampered, Reference, Settings()));
        Assert.Throws<PaymentGatewayException>(() => VnpayStatusGateway.Interpret(
            Response(("vnp_ResponseCode", "00"), ("vnp_TransactionStatus", "00"), ("vnp_TmnCode", "OTHERCOD")), Reference, Settings()));
        Assert.Throws<PaymentGatewayException>(() => VnpayStatusGateway.Interpret(
            Response(("vnp_ResponseCode", "00"), ("vnp_TransactionStatus", "00"), ("vnp_TxnRef", "OTHERREF")), Reference, Settings()));
    }

    [Fact]
    public async Task Http_error_and_missing_configuration_are_gateway_exceptions()
    {
        var forbidden = Gateway(new StubHandler(_ => (HttpStatusCode.Forbidden, "<html>403</html>")));
        await Assert.ThrowsAsync<PaymentGatewayException>(() => forbidden.QueryAsync(new GatewayStatusQuery(Reference, Created, "x"), Ct));

        var unconfigured = new VnpayStatusGateway(new HttpClient(new StubHandler(_ => (HttpStatusCode.OK, "{}"))),
            Options.Create(new VnpayOptions()), new FixedClock(Now));
        Assert.False(unconfigured.IsConfigured);
        await Assert.ThrowsAsync<PaymentGatewayException>(() => unconfigured.QueryAsync(new GatewayStatusQuery(Reference, Created, "x"), Ct));
    }

    private static VnpayStatusGateway Gateway(StubHandler handler) =>
        new(new HttpClient(handler), Options.Create(Settings()), new FixedClock(Now));

    // Phản hồi querydr mẫu, ký đúng thứ tự trường của VNPAY; overrides thay giá trị mặc định.
    private static string Response(params (string Key, string Value)[] overrides)
    {
        var fields = new Dictionary<string, string>
        {
            ["vnp_ResponseId"] = "resp-1",
            ["vnp_Command"] = "querydr",
            ["vnp_ResponseCode"] = "00",
            ["vnp_Message"] = "QueryDR Success",
            ["vnp_TmnCode"] = TmnCode,
            ["vnp_TxnRef"] = Reference,
            ["vnp_Amount"] = "29000000",
            ["vnp_BankCode"] = "NCB",
            ["vnp_PayDate"] = "20261116090500",
            ["vnp_TransactionNo"] = "14100001",
            ["vnp_TransactionType"] = "01",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_OrderInfo"] = "Truy van PI-1",
            ["vnp_PromotionCode"] = "",
            ["vnp_PromotionAmount"] = "",
        };
        foreach (var (key, value) in overrides)
            fields[key] = value;
        fields["vnp_SecureHash"] = VnpaySignature.Sign(Secret, string.Join('|',
            fields["vnp_ResponseId"], fields["vnp_Command"], fields["vnp_ResponseCode"], fields["vnp_Message"], fields["vnp_TmnCode"],
            fields["vnp_TxnRef"], fields["vnp_Amount"], fields["vnp_BankCode"], fields["vnp_PayDate"], fields["vnp_TransactionNo"],
            fields["vnp_TransactionType"], fields["vnp_TransactionStatus"], fields["vnp_OrderInfo"], fields["vnp_PromotionCode"],
            fields["vnp_PromotionAmount"]));
        return JsonSerializer.Serialize(fields);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var (status, body) = respond(request);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
