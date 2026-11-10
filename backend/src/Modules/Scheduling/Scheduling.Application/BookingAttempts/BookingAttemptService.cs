using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.BookingAttempts;

/// <summary>
/// Tạo BookingAttempt (Giải pháp v2.14 §2.1): Backend cấp BookingAttemptId ổn định; gửi lặp cùng Idempotency-Key trả cùng Id.
/// Bên gọi (endpoint) phải kiểm AppUser được đại diện PatientId trước khi gọi.
/// </summary>
public sealed class BookingAttemptService(IBookingAttemptStore store, IUnitOfWork uow, IIdempotencyStore idempotency, IDbClock dbClock)
{
    public const string OperationType = "Scheduling.CreateBookingAttempt";

    public Task<CreateAttemptOutcome> CreateAsync(CreateAttemptRequest request, CancellationToken ct)
    {
        if (request.AppUserId == Guid.Empty || request.PatientId == Guid.Empty || request.ServiceDate == DateOnly.MinValue
            || !Idempotent.IsValidKey(request.IdempotencyKey))
            return Task.FromResult(new CreateAttemptOutcome(CreateAttemptResult.InvalidInput));

        return uow.ExecuteAsync(async t =>
        {
            var scope = Idempotent.Scope(request.AppUserId);
            var hash = Idempotent.Hash(request.PatientId, request.ServiceDate.ToString("yyyy-MM-dd"));
            switch (await idempotency.BeginAsync(scope, OperationType, request.IdempotencyKey, hash, Idempotent.Retention, null, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return Idempotent.Deserialize<CreateAttemptOutcome>(replay.BodyJson);
                case IdempotencyBeginResult.InProgress:
                    return new CreateAttemptOutcome(CreateAttemptResult.InProgress);
                case IdempotencyBeginResult.Conflict:
                    return new CreateAttemptOutcome(CreateAttemptResult.IdempotencyConflict);
            }

            var now = await dbClock.GetUtcNowAsync(t);
            var attempt = new BookingAttempt
            {
                Id = Guid.CreateVersion7(),
                AppUserId = request.AppUserId,
                PatientId = request.PatientId,
                CurrentStep = CurrentStep.DoctorSelection,
                SelectedServiceDate = request.ServiceDate,
                Status = BookingAttemptStatus.Draft,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            await store.AddAsync(attempt, t);
            var outcome = new CreateAttemptOutcome(CreateAttemptResult.Created, attempt.Id);
            await idempotency.CompleteAsync(scope, OperationType, request.IdempotencyKey, 201, Idempotent.Serialize(outcome), null, t);
            return outcome;
        }, ct);
    }
}

public sealed record CreateAttemptRequest(Guid AppUserId, Guid PatientId, DateOnly ServiceDate, string IdempotencyKey);

public enum CreateAttemptResult { Created, InvalidInput, InProgress, IdempotencyConflict }

public sealed record CreateAttemptOutcome(CreateAttemptResult Result, Guid? BookingAttemptId = null);

public interface IBookingAttemptStore
{
    Task AddAsync(BookingAttempt attempt, CancellationToken ct);
}
