using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.Appointments;
using Scheduling.Application.Holds;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Xunit;

namespace SqlServerTests;

// W2-07: hủy đúng một lần, đổi lịch nguyên tử; mốc 05:00 = RescheduleCutoffTimeUtc so với giờ DB.
public sealed partial class SchedulingHoldTests
{
    [Fact]
    public async Task Patient_cancel_before_0500_releases_the_slot_once()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var booked = await BookedAsync(services, day);
            var request = PatientCancel(booked);

            var first = await CancelAsync(services, request);
            var resent = await CancelAsync(services, request);
            var newKey = await CancelAsync(services, request with { IdempotencyKey = Key() });

            Assert.Equal(ChangeResult.Cancelled, first.Result);
            Assert.True(first.Cancelled!.SlotReleased);
            Assert.Equal(first, resent);
            Assert.Equal(ChangeResult.InvalidAppointmentState, newKey.Result);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 0, 0), (quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount));
            await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Cancelled, claimActive: false);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{booked.AppointmentId}' AND EventType = 'AppointmentCancelled'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM workflow.Operation WHERE TargetEntityId = '{booked.AppointmentId}' AND OperationType = 'CancelBooking' AND Status = 'Succeeded'"));
            Assert.Equal(HoldResult.Held, (await HoldAsync(services, day, await SeedAttemptAsync(services, booked.PatientId), booked.PatientId)).Result);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Patient_cancel_from_0500_locks_the_slot_and_keeps_the_claim()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var booked = await BookedAsync(services, day);
            await SetRescheduleCutoffToNowAsync(day); // giờ DB lúc hủy ≥ 05:00

            var cancelled = await CancelAsync(services, PatientCancel(booked));

            Assert.Equal(ChangeResult.Cancelled, cancelled.Result);
            Assert.False(cancelled.Cancelled!.SlotReleased);
            var quota = await ReadQuotaAsync(services, day);
            Assert.Equal((0, 1), (quota.AdmittedCount, quota.LockedCancelledCount));
            await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Cancelled, claimActive: true);
            // Không đặt lại cùng bác sĩ/ngày để lách hạn tự đổi.
            Assert.Equal(HoldResult.AlreadyClaimed, (await HoldAsync(services, day, await SeedAttemptAsync(services, booked.PatientId), booked.PatientId)).Result);
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Checked_in_appointment_is_cancelled_only_by_receptionist_with_reason_and_audit()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var booked = await BookedAsync(services, day);
            await SetRescheduleCutoffToNowAsync(day);
            await ExecAsync($"UPDATE scheduling.Appointment SET Status = 'CheckedIn' WHERE Id = '{booked.AppointmentId}'");
            var receptionist = new CancelRequest(booked.AppointmentId, Guid.NewGuid(), CancellationInitiator.Receptionist, null, null, null, Key());

            Assert.Equal(ChangeResult.AppointmentNotFound, (await CancelAsync(services, PatientCancel(booked) with { PatientId = Guid.NewGuid() })).Result);
            Assert.Equal(ChangeResult.InvalidAppointmentState, (await CancelAsync(services, PatientCancel(booked))).Result);
            Assert.Equal(ChangeResult.ReasonRequired, (await CancelAsync(services, receptionist)).Result);
            var cancelled = await CancelAsync(services, receptionist with { Reason = "Bệnh nhân yêu cầu", IdempotencyKey = Key() });

            Assert.Equal(ChangeResult.Cancelled, cancelled.Result);
            Assert.Equal((0, 1), ((await ReadQuotaAsync(services, day)).AdmittedCount, (await ReadQuotaAsync(services, day)).LockedCancelledCount));
            Assert.Equal(1, await CountAsync($"""
                SELECT COUNT(*) FROM shared.AuditLog WHERE RecordId = '{booked.AppointmentId}' AND ActionType = 'CANCEL'
                  AND ChangeReason = N'Bệnh nhân yêu cầu' AND OperationId = '{cancelled.OperationId}'
                """));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Concurrent_cancels_with_different_keys_take_effect_once()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        try
        {
            var booked = await BookedAsync(services, day);

            var results = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => Task.Run(() => CancelAsync(services, PatientCancel(booked)), Ct)));

            Assert.Single(results, x => x.Result == ChangeResult.Cancelled);
            Assert.Equal(4, results.Count(x => x.Result == ChangeResult.InvalidAppointmentState));
            Assert.Equal((0, 0), ((await ReadQuotaAsync(services, day)).AdmittedCount, (await ReadQuotaAsync(services, day)).LockedCancelledCount));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{booked.AppointmentId}' AND EventType = 'AppointmentCancelled'"));
        }
        finally
        {
            await CleanupAsync(day);
        }
    }

    [Fact]
    public async Task Reschedule_moves_the_appointment_atomically_to_a_same_fee_doctor()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        var target = await SeedDayAsync(services);
        try
        {
            var booked = await BookedAsync(services, day);

            var moved = await RescheduleAsync(services, Reschedule(booked, target));

            Assert.Equal(ChangeResult.Rescheduled, moved.Result);
            var newId = moved.Rescheduled!.AppointmentId;
            Assert.Equal(0, (await ReadQuotaAsync(services, day)).AdmittedCount);
            Assert.Equal(1, (await ReadQuotaAsync(services, target)).AdmittedCount);
            await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Rescheduled, claimActive: false);
            await AssertAppointmentAsync(services, newId, AppointmentStatus.Scheduled, claimActive: true);
            await using var scope = services.CreateAsyncScope();
            var replacement = await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<Appointment>().SingleAsync(x => x.Id == newId, Ct);
            Assert.Equal((booked.AppointmentId, ExamFee, target.Id), (replacement.RescheduledFromAppointmentId, replacement.AppliedConsultationFee, replacement.SessionQuotaId));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{newId}' AND EventType = 'AppointmentConfirmed'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{booked.AppointmentId}' AND EventType = 'AppointmentCancelled'"));
        }
        finally
        {
            await CleanupAsync(day, target);
        }
    }

    [Fact]
    public async Task Rejected_reschedule_keeps_the_old_appointment_intact()
    {
        await using var services = BuildServices();
        var day = await SeedDayAsync(services);
        var target = await SeedDayAsync(services);
        var full = await SeedDayAsync(services, capacity: 0);
        try
        {
            var booked = await BookedAsync(services, day);

            Assert.Equal(ChangeResult.DifferentFee, (await RescheduleAsync(services, Reschedule(booked, target) with { TargetDoctorRankId = ExpensiveRank })).Result);
            Assert.Equal(ChangeResult.Full, (await RescheduleAsync(services, Reschedule(booked, full))).Result);
            Assert.Equal(ChangeResult.SameDay, (await RescheduleAsync(services, Reschedule(booked, day))).Result);
            await SetRescheduleCutoffToNowAsync(day);
            Assert.Equal(ChangeResult.RescheduleCutoffPassed, (await RescheduleAsync(services, Reschedule(booked, target))).Result);

            Assert.Equal(1, (await ReadQuotaAsync(services, day)).AdmittedCount);
            Assert.Equal(0, (await ReadQuotaAsync(services, target)).AdmittedCount);
            await AssertAppointmentAsync(services, booked.AppointmentId, AppointmentStatus.Scheduled, claimActive: true);
        }
        finally
        {
            await CleanupAsync(day, target, full);
        }
    }

    [Fact]
    public async Task Crossed_reschedules_between_two_days_both_succeed()
    {
        await using var services = BuildServices();
        var dayA = await SeedDayAsync(services);
        var dayB = await SeedDayAsync(services);
        try
        {
            var onA = await BookedAsync(services, dayA);
            var onB = await BookedAsync(services, dayB);

            // Hai lệnh khóa cùng cặp DoctorDay theo thứ tự Id nên chờ nhau, không deadlock.
            var results = await Task.WhenAll(
                Task.Run(() => RescheduleAsync(services, Reschedule(onA, dayB)), Ct),
                Task.Run(() => RescheduleAsync(services, Reschedule(onB, dayA)), Ct));

            Assert.All(results, x => Assert.Equal(ChangeResult.Rescheduled, x.Result));
            Assert.Equal(1, (await ReadQuotaAsync(services, dayA)).AdmittedCount);
            Assert.Equal(1, (await ReadQuotaAsync(services, dayB)).AdmittedCount);
        }
        finally
        {
            await CleanupAsync(dayA, dayB);
        }
    }

    // ---------- helpers W2-07 ----------

    private sealed record Booked(Guid AppointmentId, Guid PatientId);

    private static string Key() => Guid.NewGuid().ToString("N");

    private static async Task<Booked> BookedAsync(ServiceProvider services, DoctorDayQuota day)
    {
        var (attempt, held) = await HeldAttemptAsync(services, day);
        await HandleAsync(services, Paid(attempt));
        await using var scope = services.CreateAsyncScope();
        var appointment = await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<Appointment>()
            .SingleAsync(x => x.ReservationId == held.ReservationId, Ct);
        return new Booked(appointment.Id, appointment.PatientId);
    }

    private static CancelRequest PatientCancel(Booked booked) =>
        new(booked.AppointmentId, Guid.NewGuid(), CancellationInitiator.Patient, booked.PatientId, null, null, Key());

    private static RescheduleRequest Reschedule(Booked booked, DoctorDayQuota target) =>
        new(booked.AppointmentId, Guid.NewGuid(), booked.PatientId, target.DoctorId, Guid.NewGuid(), target.ServiceDate, null, Key());

    private static async Task<ChangeOutcome> CancelAsync(ServiceProvider services, CancelRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await ActivatorUtilities.CreateInstance<AppointmentChangeService>(scope.ServiceProvider).CancelAsync(request, Ct);
    }

    private static async Task<ChangeOutcome> RescheduleAsync(ServiceProvider services, RescheduleRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        return await ActivatorUtilities.CreateInstance<AppointmentChangeService>(scope.ServiceProvider).RescheduleAsync(request, Ct);
    }

    private static Task SetRescheduleCutoffToNowAsync(DoctorDayQuota day) =>
        ExecAsync($"UPDATE scheduling.DoctorDayQuota SET RescheduleCutoffTimeUtc = SYSUTCDATETIME() WHERE Id = '{day.Id}'");

    private static async Task AssertAppointmentAsync(ServiceProvider services, Guid appointmentId, AppointmentStatus status, bool claimActive)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var appointment = await db.Set<Appointment>().SingleAsync(x => x.Id == appointmentId, Ct);
        Assert.Equal(status, appointment.Status);
        Assert.Equal(claimActive, await db.Set<ActiveBookingClaim>().Where(x => x.AppointmentId == appointmentId).Select(x => x.IsActive).SingleAsync(Ct));
        var reservation = await db.Set<Reservation>().Where(x => x.Id == appointment.ReservationId).Select(x => x.Status).SingleAsync(Ct);
        Assert.Equal(status == AppointmentStatus.Scheduled ? ReservationStatus.Confirmed : ReservationStatus.Cancelled, reservation);
    }
}
