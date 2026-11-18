using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Abstractions;

namespace Shared.Infrastructure.Persistence;

/// <summary>DbContext của module tham gia unit of work; đăng ký bằng <c>AddUnitOfWorkParticipant&lt;TContext&gt;()</c>.</summary>
public sealed class UnitOfWorkParticipant(DbContext context)
{
    public DbContext Context { get; } = context;
}

/// <summary>
/// Mở một DbTransaction qua SharedDbContext, cho mọi participant dùng chung DbConnection/DbTransaction đó rồi commit một lần
/// (Giải pháp v2.14 §8.2). Deadlock 1205: chạy lại toàn bộ <c>work</c>, tối đa 3 lần thử lại, backoff + jitter (§3.1);
/// lỗi khác không retry (§3.3).
/// </summary>
internal sealed class UnitOfWork(SharedDbContext shared, OutboxWriter outbox, IEnumerable<UnitOfWorkParticipant> participants) : IUnitOfWork
{
    public const int MaxRetries = 3;
    private const int DeadlockErrorNumber = 1205;

    private readonly DbContext[] _participants = participants.Select(p => p.Context).Distinct().ToArray();

    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return ExecuteAsync<object?>(async token =>
        {
            await work(token);
            return null;
        }, ct);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Gọi lồng: tham gia transaction bên ngoài, để bên ngoài commit.
        if (shared.Database.CurrentTransaction is not null)
            return await work(ct);

        for (var retry = 0; ; retry++)
        {
            try
            {
                return await RunOnceAsync(work, ct);
            }
            catch (Exception ex) when (retry < MaxRetries && IsDeadlock(ex))
            {
                // Transaction đã rollback; bỏ thay đổi chưa lưu để lần chạy lại đọc dữ liệu mới (DB-06).
                foreach (var context in _participants)
                    context.ChangeTracker.Clear();
                outbox.Reset();
                await Task.Delay(TimeSpan.FromMilliseconds((50 << retry) + Random.Shared.Next(0, 50)), ct);
            }
        }
    }

    private async Task<T> RunOnceAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Dispose rollback khi chưa commit; với nạn nhân deadlock, server đã rollback và SqlClient bỏ qua.
        await using var transaction = await shared.Database.BeginTransactionAsync(ct);
        var dbTransaction = transaction.GetDbTransaction();
        try
        {
            foreach (var context in _participants)
            {
                if (context.Database.GetDbConnection() != dbTransaction.Connection)
                    context.Database.SetDbConnection(dbTransaction.Connection, contextOwnsConnection: false);
                await context.Database.UseTransactionAsync(dbTransaction, ct);
            }

            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            foreach (var context in _participants)
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
        }
    }

    private static bool IsDeadlock(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SqlException { Number: DeadlockErrorNumber })
                return true;
        }

        return false;
    }
}
