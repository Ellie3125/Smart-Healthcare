using IdentityPatients.Contracts;
using Notifications.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.BookingAttempts;

public sealed class BookingProgressService(IBookingAttemptStore attempts, IOperationTracker operations,
    IIdentityPatientsModule identity, IDbClock dbClock)
{
    public async Task<BookingProgress?> GetAttemptAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        var attempt = await GetOwnedAttemptAsync(attemptId, userId, ct);
        if (attempt is null)
            return null;

        var nowUtc = await dbClock.GetUtcNowAsync(ct);
        var status = attempt.Status;
        var step = attempt.CurrentStep;
        var expiresAtUtc = attempt.Reservation?.ExpiresAtUtc;
        if (status is BookingAttemptStatus.Held or BookingAttemptStatus.WaitingPayment
            && (attempt.Reservation is not { Status: ReservationStatus.Held } hold || hold.ExpiresAtUtc <= nowUtc))
        {
            status = BookingAttemptStatus.NeedsReselection;
            step = CurrentStep.DoctorSelection;
        }

        string[] actions = status switch
        {
            BookingAttemptStatus.Draft => ["Hold"],
            BookingAttemptStatus.Held or BookingAttemptStatus.WaitingPayment => ["ContinuePayment"],
            _ => [],
        };
        return new BookingProgress(attemptId, status.ToString(), step.ToString(), expiresAtUtc, actions);
    }

    public async Task<BookingOperationProgress?> GetOperationAsync(Guid operationId, Guid userId, CancellationToken ct)
    {
        var operation = await operations.GetAsync(operationId, ct);
        if (operation is null || operation.InitiatedByUserId != userId
            || operation.BookingAttemptId is not { } attemptId
            || operation.TargetEntityType != TargetEntityType.BookingAttempt || operation.TargetEntityId != attemptId)
            return null;

        var progress = await GetAttemptAsync(attemptId, userId, ct);
        return progress is null ? null : new BookingOperationProgress(operation.OperationId, attemptId,
            operation.OperationType.ToString(), operation.Status.ToString(), progress.Status, progress.CurrentStep,
            progress.ExpiresAtUtc, progress.AllowedActions, operation.CreatedAtUtc, operation.CompletedAtUtc);
    }

    public async Task<BookingProgressSnapshot?> GetOwnedAttemptAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        var attempt = await attempts.GetOwnedAsync(attemptId, userId, ct);
        return attempt is not null && await identity.CanAccessPatientAsync(userId, attempt.PatientId, AccessLevel.Booking, ct)
            ? attempt : null;
    }
}

public sealed record BookingProgressSnapshot(Guid PatientId, BookingAttemptStatus Status, CurrentStep CurrentStep,
    ReservationProgress? Reservation);

public sealed record ReservationProgress(ReservationStatus Status, DateTime ExpiresAtUtc);

public sealed record BookingProgress(Guid BookingAttemptId, string Status, string CurrentStep,
    DateTime? ExpiresAtUtc, IReadOnlyList<string> AllowedActions);

public sealed record BookingOperationProgress(Guid OperationId, Guid BookingAttemptId, string OperationType, string Status,
    string BookingAttemptStatus, string CurrentStep, DateTime? ExpiresAtUtc, IReadOnlyList<string> AllowedActions,
    DateTime CreatedAtUtc, DateTime? CompletedAtUtc);
