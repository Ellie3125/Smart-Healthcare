using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Quota phiên khám, lượt đặt, giữ chỗ 5 phút, lịch hẹn và claim chống đặt trùng (sơ đồ 3, 7).</summary>
internal static class BookingTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("DoctorSessionSlotQuota", t =>
        {
            t.Id();
            t.Ref("DoctorId", "Doctor");
            t.Ref("AssignedRoomId", "Room");
            t.Req<DateOnly>("ServiceDate", "date");
            t.Req<int>("Capacity", "int").Default("30");
            t.Req<int>("HeldCount", "int").Default("0");
            t.Req<int>("AdmittedCount", "int").Default("0");
            t.Req<int>("LockedCancelledCount", "int").Default("0");
            t.Utc("SessionStartTimeUtc");      // 06:30 ICT
            t.Utc("BookingCutoffTimeUtc");     // 09:30 ICT
            t.Utc("RescheduleCutoffTimeUtc");  // 05:00 ICT
            t.OptUtc("CheckInDeadlineUtc");
            t.Enum("SessionStatus", 20, "Scheduled", "InProgress", "Completed", "Cancelled").Default("N'Scheduled'");
            t.Flag("IsOpen", true);
            t.RowVersion();
            t.Rule("Capacity", "Capacity > 0");
            t.Rule("HeldCount", "HeldCount >= 0");
            t.Rule("AdmittedCount", "AdmittedCount >= 0");
            t.Rule("LockedCancelledCount", "LockedCancelledCount >= 0");
            t.Check("CK_DoctorSessionSlotQuota_NotOverbooked", "HeldCount + AdmittedCount + LockedCancelledCount <= Capacity");
            t.UniqueKey("UQ_DoctorSessionSlotQuota_DoctorDate", "DoctorId", "ServiceDate");
        });

        model.Table("BookingAttempt", t =>
        {
            t.Id();
            t.Ref("AppUserId", "AppUser");
            t.Ref("PatientId", "Patient");
            t.OptRef("SelectedDoctorId", "Doctor");
            t.OptRef("SelectedDoctorRankId", "DoctorRank");
            t.Enum("CurrentStep", 20, "ProfileSelection", "DoctorSelection", "SlotHeld", "PaymentInitiated", "Finalized", "Abandoned")
                .Default("N'ProfileSelection'");
            t.Opt<DateOnly>("SelectedServiceDate", "date");
            t.Enum("Status", 20, "Draft", "Held", "WaitingPayment", "Confirming", "Confirmed", "NeedsReselection", "PaidUnallocated", "Cancelled")
                .Default("N'Draft'");
            t.UtcNow("CreatedAtUtc");
            t.UtcNow("UpdatedAtUtc");
            t.RowVersion();
        });

        model.Table("Reservation", t =>
        {
            t.Id();
            t.Ref("SessionQuotaId", "DoctorSessionSlotQuota");
            t.Ref("BookingAttemptId", "BookingAttempt");
            t.Ref("PatientId", "Patient");
            t.Ref("AppUserId", "AppUser");
            t.Enum("BookingChannel", 10, "Web", "App", "Chatbot", "Counter");
            t.Utc("ReservedAtUtc");
            t.Utc("ExpiresAtUtc"); // ReservedAtUtc + 5 phút
            t.Enum("Status", 10, "Held", "Confirmed", "Expired", "Cancelled").Default("N'Held'");
            t.Opt<string>("IdempotencyKey", "varchar(100)");
            t.RowVersion();
            t.Check("CK_Reservation_Expiry", "ExpiresAtUtc > ReservedAtUtc");
            t.UniqueWhere("UX_Reservation_IdempotencyKey", "IdempotencyKey IS NOT NULL", "IdempotencyKey");
        });

        model.Table("Appointment", t =>
        {
            t.Id();
            t.Ref("ReservationId", "Reservation");
            t.Ref("SessionQuotaId", "DoctorSessionSlotQuota");
            t.Ref("PatientId", "Patient");
            t.OptRef("RescheduledFromAppointmentId", "Appointment", "RescheduledFrom"); // đề xuất, chưa chốt
            t.Req<string>("AppointmentNumber", "varchar(30)");
            t.Money("AppliedConsultationFee");
            t.Enum("Status", 10, "Scheduled", "CheckedIn", "Completed", "Cancelled", "Absent").Default("N'Scheduled'");
            t.Utc("ConfirmedAtUtc");
            t.OptUtc("CancelledAtUtc");
            t.Opt<string>("CancellationReason", "nvarchar(500)");
            t.RowVersion();
            t.Rule("AppliedConsultationFee", "AppliedConsultationFee >= 0");
            t.Unique("ReservationId");
            t.Unique("AppointmentNumber");
        });

        model.Table("ActiveBookingClaim", t =>
        {
            t.Id();
            t.Ref("SessionQuotaId", "DoctorSessionSlotQuota");
            t.Ref("DoctorId", "Doctor");
            t.Req<DateOnly>("ServiceDate", "date");
            t.Ref("PatientId", "Patient");
            t.Ref("ReservationId", "Reservation");
            t.OptRef("AppointmentId", "Appointment");
            t.Enum("ClaimSource", 10, "App", "Web", "Chatbot", "Counter");
            t.Flag("IsActive", true);
            t.UtcNow("CreatedAtUtc");
            t.OptUtc("ReleasedAtUtc");
            // Một bệnh nhân chỉ giữ một chỗ đang hoạt động với một bác sĩ trong một ngày.
            t.UniqueWhere("UX_ActiveBookingClaim_DoctorDatePatient", "IsActive = 1", "DoctorId", "ServiceDate", "PatientId");
        });
    }
}
