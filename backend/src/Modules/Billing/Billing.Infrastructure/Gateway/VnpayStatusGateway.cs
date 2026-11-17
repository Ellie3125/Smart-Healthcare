using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Application;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Gateway;

/// <summary>
/// API truy vấn giao dịch của VNPAY (vnp_Command=querydr, phiên bản 2.1.0). Chữ ký HMAC-SHA512 trên các giá trị nối bằng "|"
/// theo thứ tự cố định của tài liệu VNPAY (khác chữ ký của URL thanh toán). vnp_ResponseCode là kết quả của lần truy vấn,
/// vnp_TransactionStatus mới là kết quả thanh toán.
/// </summary>
internal sealed class VnpayStatusGateway(HttpClient http, IOptions<VnpayOptions> options, TimeProvider clock) : IPaymentStatusGateway
{
    private const string Version = "2.1.0";
    private const string Command = "querydr";
    private const string ServerIp = "127.0.0.1";

    private static readonly string[] ResponseHashOrder =
    [
        "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode",
        "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode",
        "vnp_PromotionAmount",
    ];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.TmnCode) && !string.IsNullOrWhiteSpace(options.Value.HashSecret)
        && !string.IsNullOrWhiteSpace(options.Value.QueryUrl);

    public async Task<GatewayPaymentStatus> QueryAsync(GatewayStatusQuery query, CancellationToken ct)
    {
        var settings = options.Value;
        if (!IsConfigured)
            throw new PaymentGatewayException("VNPAY chưa cấu hình TmnCode, HashSecret hoặc QueryUrl.");

        var request = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"),
            ["vnp_Version"] = Version,
            ["vnp_Command"] = Command,
            ["vnp_TmnCode"] = settings.TmnCode,
            ["vnp_TxnRef"] = query.TransactionReference,
            ["vnp_OrderInfo"] = query.Description,
            ["vnp_TransactionDate"] = VnpayTime.Format(query.CreatedAtUtc),
            ["vnp_CreateDate"] = VnpayTime.Format(clock.GetUtcNow().UtcDateTime),
            ["vnp_IpAddr"] = ServerIp,
        };
        request[VnpaySignature.HashParameter] = VnpaySignature.Sign(settings.HashSecret, string.Join('|',
            request["vnp_RequestId"], request["vnp_Version"], request["vnp_Command"], request["vnp_TmnCode"], request["vnp_TxnRef"],
            request["vnp_TransactionDate"], request["vnp_CreateDate"], request["vnp_IpAddr"], request["vnp_OrderInfo"]));

        string raw;
        try
        {
            using var response = await http.PostAsJsonAsync(settings.QueryUrl, request, ct);
            raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new PaymentGatewayException($"VNPAY querydr trả HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PaymentGatewayException("Không gọi được VNPAY querydr.", ex);
        }

        return Interpret(raw, query.TransactionReference, settings);
    }

    internal static GatewayPaymentStatus Interpret(string raw, string transactionReference, VnpayOptions settings)
    {
        var fields = Parse(raw);
        var responseCode = Get(fields, "vnp_ResponseCode");

        // 00: truy vấn thành công; 91: không có giao dịch. Mã khác (02, 03, 94, 97, 99) là lỗi của lần truy vấn, không nói gì về tiền.
        if (responseCode is not ("00" or "91"))
            throw new PaymentGatewayException($"VNPAY querydr từ chối: {responseCode} {Get(fields, "vnp_Message")}".Trim());

        var expected = VnpaySignature.Sign(settings.HashSecret, string.Join('|', ResponseHashOrder.Select(k => Get(fields, k))));
        var received = Get(fields, VnpaySignature.HashParameter).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received)))
            throw new PaymentGatewayException("Chữ ký phản hồi querydr của VNPAY không hợp lệ.");
        if (Get(fields, "vnp_TmnCode") != settings.TmnCode)
            throw new PaymentGatewayException("Phản hồi querydr không phải của mã website này.");

        if (responseCode == "91")
            return new GatewayPaymentStatus(GatewayPaymentState.NotFound, null, null, null, "91", raw);

        if (Get(fields, "vnp_TxnRef") != transactionReference)
            throw new PaymentGatewayException("Phản hồi querydr mang mã tham chiếu khác.");

        var status = Get(fields, "vnp_TransactionStatus");
        var transactionNo = Get(fields, "vnp_TransactionNo") is { Length: > 0 } no && no != "0" ? no : null;
        decimal? amount = decimal.TryParse(Get(fields, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            ? minor / 100m
            : null;
        var state = status switch
        {
            "00" => GatewayPaymentState.Paid,
            "01" => GatewayPaymentState.Pending,
            "02" => GatewayPaymentState.Failed,
            // 04: giao dịch đảo (đã trừ tiền ở ngân hàng); 07: nghi gian lận; mã khác: chưa rõ → người xử lý.
            _ => GatewayPaymentState.NeedsReview,
        };
        return new GatewayPaymentStatus(state, transactionNo, amount, VnpayTime.ParseUtc(Get(fields, "vnp_PayDate")), status, raw);
    }

    private static Dictionary<string, string> Parse(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? "",
                    JsonValueKind.Null => "",
                    _ => p.Value.GetRawText(),
                },
                StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("Phản hồi querydr của VNPAY không phải JSON.", ex);
        }
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string key) => fields.TryGetValue(key, out var value) ? value : "";
}
