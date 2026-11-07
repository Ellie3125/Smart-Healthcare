using Billing.Infrastructure.Gateway;
using Xunit;

namespace Billing.Tests;

public class VnpaySignatureTests
{
    private const string Secret = "test-hash-secret-0123456789";

    // Vector tính độc lập bằng Python: quote_plus, sắp theo tên tham số, hmac sha512.
    private const string Hash = "57bc7ce36e582ffbd086f52669fb057cdba4d2cde8e1a7e0536ea3a9c3eeb9d4cd924b03de9e221f8e92de7b5aa52f2edc17c73c74fe62d1a9043fbdf250a350";

    private static Dictionary<string, string> Parameters() => new()
    {
        ["vnp_TxnRef"] = "REF0001",
        ["vnp_Amount"] = "15000000",
        ["vnp_TmnCode"] = "TESTCODE",
        ["vnp_BankCode"] = "NCB",
        ["vnp_ResponseCode"] = "00",
        ["vnp_OrderInfo"] = "Thanh toan phi kham ban dau PI-20261105-ABC",
        ["vnp_ReturnUrl"] = "http://127.0.0.1:5080/dev/billing/vnpay/return",
    };

    [Fact]
    public void Matches_a_vector_computed_independently()
    {
        var canonical = VnpaySignature.Canonical(Parameters());

        Assert.Equal(
            "vnp_Amount=15000000&vnp_BankCode=NCB&vnp_OrderInfo=Thanh+toan+phi+kham+ban+dau+PI-20261105-ABC&vnp_ResponseCode=00"
            + "&vnp_ReturnUrl=http%3A%2F%2F127.0.0.1%3A5080%2Fdev%2Fbilling%2Fvnpay%2Freturn&vnp_TmnCode=TESTCODE&vnp_TxnRef=REF0001",
            canonical);
        Assert.Equal(Hash, VnpaySignature.Sign(Secret, canonical));
    }

    [Fact]
    public void Accepts_the_hash_in_either_case_and_ignores_parameters_outside_the_signature()
    {
        var parameters = Parameters();
        parameters["vnp_SecureHash"] = Hash.ToUpperInvariant();
        parameters["vnp_SecureHashType"] = "HmacSHA512";
        parameters["vnp_BankTranNo"] = "";
        parameters["utm_source"] = "campaign";

        Assert.True(VnpaySignature.Verify(Secret, parameters));
    }

    [Theory]
    [InlineData("tampered amount")]
    [InlineData("wrong secret")]
    [InlineData("no hash")]
    [InlineData("short hash")]
    public void Rejects_anything_else(string reason)
    {
        var parameters = Parameters();
        parameters["vnp_SecureHash"] = Hash;
        var secret = Secret;
        switch (reason)
        {
            case "tampered amount": parameters["vnp_Amount"] = "100"; break;
            case "wrong secret": secret = "another-secret"; break;
            case "no hash": parameters.Remove("vnp_SecureHash"); break;
            default: parameters["vnp_SecureHash"] = Hash[..64]; break;
        }

        Assert.False(VnpaySignature.Verify(secret, parameters));
    }
}
