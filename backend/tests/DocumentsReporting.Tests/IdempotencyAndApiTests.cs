using System.Security.Claims;
using System.Text;
using DocumentsReporting.Api;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Http;
using Shared.Abstractions;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class IdempotencyAndApiTests
{
    private readonly TestResultAttachmentStore _store = new();
    private readonly InMemoryObjectStorage _storage = new();
    private readonly TestSchedulingModule _scheduling = new();
    private readonly TestClinicalModule _clinical = new();
    private readonly TestIdentityPatientsModule _identity = new();
    private readonly TestAuditLogPort _auditLog = new();
    private readonly TestTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private DocumentsService CreateService() =>
        new(_store, _storage, _scheduling, _clinical, _identity, _auditLog, _clock);

    private static FormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    [Fact]
    public async Task Idempotency_SameKeyAndContent_ReplaysResponse()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var uow = new FakeUnitOfWork();
        var documentsModule = CreateService();

        var queueTicketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, Roles.RoomAccount),
            new Claim(AppClaims.RoomId, roomId.ToString()),
            new Claim(AppClaims.Channel, Channels.Staff)
        ], "TestAuth"));

        // Gọi lần 1
        var file1 = CreateFormFile("ketqua.pdf", "%PDF-1.7\nSample data");
        var res1 = await CallUploadEndpointAsync(
            queueTicketId, file1, "KEY-123", "DEV-01", null, user, documentsModule, idempotencyStore, uow);

        Assert.Equal(200, res1.StatusCode);
        Assert.Single(_store.AllAttachments);

        // Gọi lần 2 với cùng key và content
        var file2 = CreateFormFile("ketqua.pdf", "%PDF-1.7\nSample data");
        var res2 = await CallUploadEndpointAsync(
            queueTicketId, file2, "KEY-123", "DEV-01", null, user, documentsModule, idempotencyStore, uow);

        Assert.Equal(200, res2.StatusCode);
        // Không tạo thêm attachment thứ hai
        Assert.Single(_store.AllAttachments);
    }

    [Fact]
    public async Task Idempotency_SameKeyDifferentContent_ReturnsConflict()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var uow = new FakeUnitOfWork();
        var documentsModule = CreateService();

        var queueTicketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, Roles.RoomAccount),
            new Claim(AppClaims.RoomId, roomId.ToString()),
            new Claim(AppClaims.Channel, Channels.Staff)
        ], "TestAuth"));

        // Lần 1: gửi tệp A
        var fileA = CreateFormFile("a.pdf", "%PDF-1.7\nContent A");
        var resA = await CallUploadEndpointAsync(
            queueTicketId, fileA, "KEY-CONFLICT", "DEV-01", null, user, documentsModule, idempotencyStore, uow);
        Assert.Equal(200, resA.StatusCode);

        // Lần 2: gửi cùng KEY-CONFLICT nhưng tệp B (khác hash)
        var fileB = CreateFormFile("b.pdf", "%PDF-1.7\nContent B (completely different)");
        var resB = await CallUploadEndpointAsync(
            queueTicketId, fileB, "KEY-CONFLICT", "DEV-01", null, user, documentsModule, idempotencyStore, uow);

        Assert.Equal(409, resB.StatusCode);
    }

    private static async Task<(int StatusCode, object? Value)> CallUploadEndpointAsync(
        Guid queueTicketId,
        IFormFile file,
        string? idempotencyKey,
        string? deviceId,
        string? sessionId,
        ClaimsPrincipal user,
        IDocumentsModule documentsModule,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork uow)
    {
        // Reflection gọi private static method UploadResultFileAsync của DocumentsEndpoints
        var method = typeof(DocumentsEndpoints).GetMethod("UploadResultFileAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var task = (Task<IResult>)method.Invoke(null, [
            queueTicketId,
            file,
            idempotencyKey,
            deviceId,
            sessionId,
            user,
            documentsModule,
            idempotencyStore,
            uow,
            CancellationToken.None
        ])!;

        var result = await task;

        if (result is Microsoft.AspNetCore.Http.HttpResults.Ok<StoredFileDto> okResult)
            return (200, okResult.Value);

        if (result is Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem)
            return (problem.StatusCode, problem.ProblemDetails);

        return (200, null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) =>
            work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            work(ct);
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, (string Hash, int? Status, string? Body)> _map = new();

        public Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key, string requestHash, TimeSpan ttl, Guid? operationId, CancellationToken ct)
        {
            var compoundKey = $"{callerScope}:{operationType}:{key}";
            if (_map.TryGetValue(compoundKey, out var existing))
            {
                if (existing.Hash != requestHash)
                    return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.Conflict());

                if (existing.Status.HasValue)
                    return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.Replay(existing.Status.Value, existing.Body, null));

                return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(null));
            }

            _map[compoundKey] = (requestHash, null, null);
            return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.Started());
        }

        public Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson, Guid? operationId, CancellationToken ct)
        {
            var compoundKey = $"{callerScope}:{operationType}:{key}";
            if (_map.TryGetValue(compoundKey, out var existing))
            {
                _map[compoundKey] = (existing.Hash, statusCode, bodyJson);
            }

            return Task.CompletedTask;
        }
    }
}
