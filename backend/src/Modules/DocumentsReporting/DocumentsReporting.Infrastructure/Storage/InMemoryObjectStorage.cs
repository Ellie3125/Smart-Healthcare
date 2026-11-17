using System.Collections.Concurrent;
using DocumentsReporting.Application;

namespace DocumentsReporting.Infrastructure.Storage;

/// <summary>
/// Triển khai IObjectStorage trong bộ nhớ phục vụ kiểm thử và chạy local không có cấu hình Cloudflare R2.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _storage = new();

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _storage[key] = (ms.ToArray(), contentType);
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_storage.TryGetValue(key, out var item))
        {
            throw new FileNotFoundException($"Không tìm thấy đối tượng trên bộ nhớ giả lập với key: {key}");
        }

        var stream = new MemoryStream(item.Data, writable: false);
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _storage.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Task.FromResult(_storage.ContainsKey(key));
    }

    public void Clear() => _storage.Clear();
}
