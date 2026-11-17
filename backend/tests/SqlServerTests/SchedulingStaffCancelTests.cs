using Scheduling.Application.Appointments;
using Scheduling.Contracts;
using Xunit;

namespace SqlServerTests;

// Tiếp tân hủy lịch đã check in (Phạm vi §6.2): chỉ khi bệnh nhân còn chờ khám đầu; hủy vé chờ và nhả trạm cùng transaction.
public sealed partial class SchedulingHoldTests
{
    [Fact]
    public async Task Receptionist_cancel_of_checked_in_patient_cancels_called_ticket_but_refuses_patient_in_service()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock); var station = Guid.NewGuid();
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, [station]);
            var called = await BookedAsync(services, day);
            var calledTicket = (await CheckInAsync(services, called.AppointmentId)).QueueTicketId;
            Assert.Equal(calledTicket, (await CallNextAsync(services, day, station)).Ticket!.QueueTicketId);
            var serving = await BookedAsync(services, day);
            var servingTicket = (await CheckInAsync(services, serving.AppointmentId)).QueueTicketId;

            var cancelled = await CancelAsync(services, Receptionist(called.AppointmentId));

            Assert.Equal(ChangeResult.Cancelled, cancelled.Result);
            Assert.False(cancelled.Cancelled!.SlotReleased); // sau 05:00: khóa suất, không hoàn
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE Id = '{calledTicket}' AND QueueStatus = 'Cancelled' AND CompletedAtUtc IS NOT NULL"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveStationAllocation WHERE CurrentTicketId = '{calledTicket}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActivePatientEngagement WHERE CurrentTicketId = '{calledTicket}'"));
            Assert.Equal(1, await CountAsync($"""
                SELECT COUNT(*) FROM shared.AuditLog WHERE RecordId = '{called.AppointmentId}' AND ActionType = 'CANCEL'
                  AND NewValuesJson LIKE '%{calledTicket}%'
                """));

            await ExecAsync($"UPDATE scheduling.QueueTicket SET QueueStatus = 'InProgress' WHERE Id = '{servingTicket}'");
            var refused = await CancelAsync(services, Receptionist(serving.AppointmentId));

            Assert.Equal(ChangeResult.PatientInService, refused.Result);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Appointment WHERE Id = '{serving.AppointmentId}' AND Status = 'CheckedIn'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE Id = '{servingTicket}' AND QueueStatus = 'InProgress'"));
            Assert.Equal(1, (await ReadQuotaAsync(services, day)).LockedCancelledCount);
        }
        finally { await CleanupQueueAsync(day); }
    }

    private static CancelRequest Receptionist(Guid appointmentId) =>
        new(appointmentId, Guid.NewGuid(), CancellationInitiator.Receptionist, null, "Bệnh nhân xin về", null, Key());
}
