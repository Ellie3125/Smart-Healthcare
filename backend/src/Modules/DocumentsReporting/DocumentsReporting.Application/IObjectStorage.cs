namespace DocumentsReporting.Application;

/// <summary>
/// Cổng giao tiếp với Object Storage (Cloudflare R2 private hoặc bản giả lập InMemory) (GP §6.4, §8.6).
/// Tất cả thao tác tải tệp chạy NGOÀI transaction DB.
/// </summary>
public interface IObjectStorage
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
