using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocumentsReporting.Application;
using Microsoft.Extensions.Logging;

namespace DocumentsReporting.Infrastructure.Storage;

/// <summary>
/// Cài đặt IObjectStorage kết nối tới Cloudflare R2 bucket private qua S3-compatible API (GP §6.4, §8.6).
/// Thực hiện AWS Signature Version 4 chuẩn mực.
/// </summary>
public sealed class R2ObjectStorage(HttpClient httpClient, R2Options options, ILogger<R2ObjectStorage> logger) : IObjectStorage
{
    private readonly string _region = "auto";
    private readonly string _service = "s3";

    private string ServiceEndpoint => !string.IsNullOrWhiteSpace(options.ServiceUrl)
        ? options.ServiceUrl.TrimEnd('/')
        : $"https://{options.AccountId}.r2.cloudflarestorage.com";

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);

        // Copy content để hash và gửi
        byte[] payloadBytes;
        if (content is MemoryStream ms && ms.TryGetBuffer(out var buffer))
        {
            payloadBytes = buffer.ToArray();
        }
        else
        {
            using var mem = new MemoryStream();
            await content.CopyToAsync(mem, ct);
            payloadBytes = mem.ToArray();
        }

        var url = $"{ServiceEndpoint}/{options.BucketName}/{key.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = new ByteArrayContent(payloadBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        SignRequest(request, payloadBytes);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Upload R2 lỗi kết nối, key {Key}.", key);
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Upload R2 lỗi {Status}, key {Key}: {Body}", (int)response.StatusCode, key, errorBody);
                throw new InvalidOperationException($"Lỗi tải tệp lên Cloudflare R2: Status {response.StatusCode}, Chi tiết: {errorBody}");
            }
        }
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var url = $"{ServiceEndpoint}/{options.BucketName}/{key.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        SignRequest(request, []);

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new FileNotFoundException($"Không tìm thấy tệp trên Cloudflare R2: {key}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            throw new InvalidOperationException($"Lỗi tải tệp từ Cloudflare R2: Status {response.StatusCode}, Chi tiết: {errorBody}");
        }

        // Trả luồng dữ liệu stream
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var url = $"{ServiceEndpoint}/{options.BucketName}/{key.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);

        SignRequest(request, []);

        using var response = await httpClient.SendAsync(request, ct);
        // Delete trên S3 là idempotent, 204 hoặc 404 coi như xong
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var url = $"{ServiceEndpoint}/{options.BucketName}/{key.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Head, url);

        SignRequest(request, []);

        using var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private void SignRequest(HttpRequestMessage request, byte[] payloadBytes)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.Host = request.RequestUri!.Host;

        var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var canonicalUri = request.RequestUri.AbsolutePath;
        var canonicalHeaders = $"host:{request.Headers.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = $"{request.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var canonicalRequestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

        var credentialScope = $"{dateStamp}/{_region}/{_service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        var signingKey = GetSignatureKey(options.SecretAccessKey!, dateStamp, _region, _service);
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));

        var authHeader = $"AWS4-HMAC-SHA256 Credential={options.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(regionName));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(serviceName));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }
}
