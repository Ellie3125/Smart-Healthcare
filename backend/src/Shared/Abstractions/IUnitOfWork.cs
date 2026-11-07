namespace Shared.Abstractions;

/// <summary>
/// Unit of work cho phép nhiều DbContext tham gia cùng một DbTransaction.
/// TODO: Implement đầy đủ ở tuần 2 khi có cross-module transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
    Task ExecuteAsync(Func<Task> action, CancellationToken ct = default);
}

