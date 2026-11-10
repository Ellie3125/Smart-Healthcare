using Microsoft.EntityFrameworkCore;
using Notifications.Contracts;
using Notifications.Infrastructure.Persistence;
using Shared.Abstractions;
using Domain = Notifications.Domain;

namespace Notifications.Infrastructure.Operations;

internal sealed class EfOperationTracker(WorkflowDbContext db, IDbClock dbClock) : IOperationTracker
{
    public async Task StartAsync(StartOperation operation, CancellationToken ct)
    {
        RequireTransaction();
        db.Add(new Domain.Entities.Operation
        {
            Id = operation.OperationId,
            InitiatedByUserId = operation.InitiatedByUserId,
            BookingAttemptId = operation.BookingAttemptId,
            OperationType = Map<Domain.Enums.OperationType>(operation.OperationType),
            TargetEntityType = Map<Domain.Enums.TargetEntityType>(operation.TargetEntityType),
            TargetEntityId = operation.TargetEntityId,
            Status = Domain.Enums.OperationStatus.Running,
            CreatedAtUtc = await dbClock.GetUtcNowAsync(ct),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid operationId, OperationStatus status, string? failureReason, CancellationToken ct)
    {
        RequireTransaction();
        var operation = await db.Set<Domain.Entities.Operation>().SingleAsync(x => x.Id == operationId, ct);
        operation.Status = Map<Domain.Enums.OperationStatus>(status);
        operation.FailureReason = failureReason;
        operation.CompletedAtUtc = await dbClock.GetUtcNowAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<OperationView?> GetAsync(Guid operationId, CancellationToken ct) =>
        db.Set<Domain.Entities.Operation>().AsNoTracking()
            .Where(x => x.Id == operationId)
            .Select(x => new OperationView(x.Id, x.InitiatedByUserId, x.BookingAttemptId,
                Map<OperationType>(x.OperationType), Map<TargetEntityType>(x.TargetEntityType), x.TargetEntityId,
                Map<OperationStatus>(x.Status), x.FailureReason, x.CreatedAtUtc, x.CompletedAtUtc))
            .SingleOrDefaultAsync(ct);

    internal static TTo Map<TTo>(Enum value) where TTo : struct, Enum => Enum.Parse<TTo>(value.ToString());

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Operation phải ghi bên trong IUnitOfWork.");
    }
}
