namespace Scheduling.Infrastructure.Persistence;

internal static class AppointmentNumbers
{
    // Mã lịch hẹn, không phải số thứ tự khám (Phạm vi §4.3). 8 ký tự hex cuối của Guid v7 là phần ngẫu nhiên; unique index chặn trùng.
    public static string New(DateOnly serviceDate, Guid appointmentId) =>
        $"LH{serviceDate:yyMMdd}-{appointmentId.ToString("N")[^8..].ToUpperInvariant()}";
}
