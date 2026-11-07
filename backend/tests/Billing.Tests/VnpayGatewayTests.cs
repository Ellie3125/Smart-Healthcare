using System.Net;
using Billing.Application;
using Billing.Infrastructure.Gateway;
using Microsoft.Extensions.Options;
using Xunit;

namespace Billing.Tests;

public class VnpayGatewayTests
{
    private const string Secret = "test-hash-secret-0123456789";
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static VnpayOptions Configured() => new() { TmnCode = "TESTCODE", HashSecret = Secret, ReturnUrl = "http://127.0.0.1:5080/return" };

    private static GatewayCheckoutRequest Request(decimal amount = 150_000m, string currency = "VND", string? ip = "203.0.113.7") =>
        new("REF0001", amount, currency, "Thanh toan phi kham ban dau PI-20261105-ABC", Now.AddMinutes(15), ip);

    private static VnpayGateway Gateway(VnpayOptions options) => new(Options.Create(options), new FixedClock(Now));

    private static Dictionary<string, string> Query(string url) =>
        url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..].Split('&').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);

    [Fact]
    public async Task Builds_a_signed_url_with_amount_times_100_and_gmt7_times()
    {
        var checkout = await Gateway(Configured()).CreateCheckoutAsync(Request(), Ct);

        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", checkout.PaymentUrl, StringComparison.Ordinal);
        Assert.Contains("vnp_OrderInfo=Thanh+toan+phi+kham", checkout.PaymentUrl, StringComparison.Ordinal);
        var query = Query(checkout.PaymentUrl);
        Assert.Equal("2.1.0", query["vnp_Version"]);
        Assert.Equal("pay", query["vnp_Command"]);
        Assert.Equal("TESTCODE", query["vnp_TmnCode"]);
        Assert.Equal("15000000", query["vnp_Amount"]);
        Assert.Equal("VND", query["vnp_CurrCode"]);
        Assert.Equal("REF0001", query["vnp_TxnRef"]);
        Assert.Equal("203.0.113.7", query["vnp_IpAddr"]);
        Assert.Equal("20261105090000", query["vnp_CreateDate"]);
        Assert.Equal("20261105091500", query["vnp_ExpireDate"]);
        Assert.True(VnpaySignature.Verify(Secret, query));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("::1")]
    public async Task Short_or_missing_ip_falls_back_to_loopback(string? ip)
    {
        var checkout = await Gateway(Configured()).CreateCheckoutAsync(Request(ip: ip), Ct);

        Assert.Equal("127.0.0.1", Query(checkout.PaymentUrl)["vnp_IpAddr"]);
    }

    [Fact]
    public async Task Not_configured_or_not_payable_in_vnd_is_a_gateway_error()
    {
        var unconfigured = Gateway(new VnpayOptions());
        var configured = Gateway(Configured());

        await Assert.ThrowsAsync<PaymentGatewayException>(() => unconfigured.CreateCheckoutAsync(Request(), Ct));
        await Assert.ThrowsAsync<PaymentGatewayException>(() => configured.CreateCheckoutAsync(Request(amount: 100.5m), Ct));
        await Assert.ThrowsAsync<PaymentGatewayException>(() => configured.CreateCheckoutAsync(Request(currency: "USD"), Ct));
    }

    [Fact]
    public void Pay_date_is_read_as_gmt7()
    {
        Assert.Equal(new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc), VnpayTime.ParseUtc("20261105090000"));
        Assert.Null(VnpayTime.ParseUtc("not-a-date"));
        Assert.Null(VnpayTime.ParseUtc(null));
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
