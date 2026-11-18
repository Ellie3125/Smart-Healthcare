using System.Globalization;

namespace Scheduling.Contracts;

public static class TicketDisplayNumber
{
    public static string? Format(string? roomNumber, string ticketType, int sequenceNumber) =>
        string.IsNullOrWhiteSpace(roomNumber) ? null
            : roomNumber + "-" + (ticketType == "ReturnConsultation" ? "R" : "")
                + sequenceNumber.ToString("D3", CultureInfo.InvariantCulture);
}
