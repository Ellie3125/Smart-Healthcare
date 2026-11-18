using Billing.Contracts;
using IdentityPatients.Contracts;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Holds;
using Scheduling.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;
using System.Text.Json.Serialization;

namespace Scheduling.Application.WalkIn;

public sealed record WalkInRequest(Guid PatientId, Guid DoctorId, Guid ReceptionistUserId, string IdempotencyKey);
[JsonConverter(typeof(JsonStringEnumConverter<WalkInResult>))]
public enum WalkInResult { Registered, InvalidInput, PatientNotFound, DoctorNotFound, HoldRejected, InProgress, IdempotencyConflict }
public sealed record WalkInOutcome(WalkInResult Result, Guid? BookingAttemptId = null, string? AppointmentNumber = null,
    CheckInResult? CheckIn = null, ExamFeeCashReceipt? Receipt = null, HoldResult? HoldFailure = null);

public interface IWalkInStore
{
    Task<PaymentVerified> ReadPaymentAsync(Guid paymentIntentId, CancellationToken ct);
    Task<(Guid Id, string Number)> GetAppointmentAsync(Guid reservationId, CancellationToken ct);
}

/// <summary>Adapter tiếp nhận Identity xác minh hồ sơ tồn tại; không dùng quyền người đại diện của tài khoản tiếp tân.</summary>
public interface IWalkInPatientDirectory
{
    Task<bool> ExistsAsync(Guid patientId, CancellationToken ct);
}

public sealed class IdentityWalkInPatientDirectory(IIdentityPatientsModule identity) : IWalkInPatientDirectory
{
    public Task<bool> ExistsAsync(Guid patientId, CancellationToken ct) => identity.PatientExistsAsync(patientId, ct);
}

/// <summary>Tiếp tân đã chọn PatientId hợp lệ từ nghiệp vụ tiếp nhận Identity và đã nhận tiền mặt.</summary>
public sealed class WalkInService(BookingAttemptService attempts, HoldService holds, IBookingPaymentStore heldBookings,
    IBillingModule billing, ICounterExamFeeCollector cash, IWalkInStore store,
    IIntegrationEventHandler<PaymentVerified> confirmation, CheckInService checkIn, IIdentityPatientsModule identity,
    IDbClock clock, IUnitOfWork uow, IIdempotencyStore idempotency, IWalkInPatientDirectory patients)
{
    public const string OperationType = "Scheduling.RegisterWalkIn";

    public Task<WalkInOutcome> RegisterAsync(WalkInRequest request, CancellationToken ct)
    {
        if (request is null || request.PatientId == Guid.Empty || request.DoctorId == Guid.Empty
            || request.ReceptionistUserId == Guid.Empty || !Idempotent.IsValidKey(request.IdempotencyKey))
            return Task.FromResult(new WalkInOutcome(WalkInResult.InvalidInput));

        return uow.ExecuteAsync(async t =>
        {
            var scope = Idempotent.Scope(request.ReceptionistUserId);
            switch (await idempotency.BeginAsync(scope, OperationType, request.IdempotencyKey,
                Idempotent.Hash(request.PatientId, request.DoctorId), Idempotent.Retention, null, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return Idempotent.Deserialize<WalkInOutcome>(replay.BodyJson);
                case IdempotencyBeginResult.InProgress:
                    return new WalkInOutcome(WalkInResult.InProgress);
                case IdempotencyBeginResult.Conflict:
                    return new WalkInOutcome(WalkInResult.IdempotencyConflict);
            }

            var outcome = await RegisterOnceAsync(request, t);
            await idempotency.CompleteAsync(scope, OperationType, request.IdempotencyKey, StatusCode(outcome),
                Idempotent.Serialize(outcome), null, t);
            return outcome;
        }, ct);
    }

    private async Task<WalkInOutcome> RegisterOnceAsync(WalkInRequest request, CancellationToken ct)
    {
        if (!await patients.ExistsAsync(request.PatientId, ct))
            return new(WalkInResult.PatientNotFound);
        var doctor = await identity.GetDoctorAsync(request.DoctorId, ct);
        if (doctor is not { IsActive: true })
            return new(WalkInResult.DoctorNotFound);
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        var attempt = await attempts.CreateAsync(new(request.ReceptionistUserId, request.PatientId, today,
            Guid.CreateVersion7().ToString("N")), ct);
        var attemptId = attempt.BookingAttemptId
            ?? throw new InvalidOperationException("Không tạo được lượt đăng ký tại quầy.");
        var held = await holds.HoldAsync(new(attemptId, request.PatientId, request.ReceptionistUserId, request.DoctorId,
            doctor.RankId, today, BookingChannel.Counter, Guid.CreateVersion7().ToString("N")), ct);
        if (held.Result != HoldResult.Held)
            return new(WalkInResult.HoldRejected, attemptId, HoldFailure: held.Result);

        var booking = await heldBookings.GetLatestHoldAsync(attemptId, request.ReceptionistUserId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy giữ chỗ vừa tạo.");
        var itemName = $"Phí khám - {doctor.FullName}";
        var charge = await billing.CreateExamChargeAsync(new(request.DoctorId, doctor.RankId,
            itemName.Length <= 200 ? itemName : itemName[..200], booking.ReservedAtUtc), ct);
        var receipt = await cash.CollectExamFeeCashAsync(new(attemptId, request.PatientId, charge.ChargeId,
            request.ReceptionistUserId), ct);
        // Dùng đúng EventId Billing vừa ghi để worker replay bỏ qua cùng dấu ProcessedEvent.
        await confirmation.HandleAsync(await store.ReadPaymentAsync(receipt.PaymentIntentId, ct), ct);
        var appointment = await store.GetAppointmentAsync(held.ReservationId!.Value, ct);
        var checkedIn = await checkIn.CheckInAsync(new(appointment.Id, request.ReceptionistUserId, held.OperationId), ct);
        return new(WalkInResult.Registered, attemptId, appointment.Number, checkedIn, receipt);
    }

    public static int StatusCode(WalkInOutcome outcome) => outcome.Result switch
    {
        WalkInResult.Registered => 201,
        WalkInResult.InvalidInput => 400,
        WalkInResult.PatientNotFound or WalkInResult.DoctorNotFound => 404,
        WalkInResult.HoldRejected when outcome.HoldFailure is HoldResult.DayNotFound or HoldResult.DoctorNotFound => 404,
        _ => 409,
    };
}
