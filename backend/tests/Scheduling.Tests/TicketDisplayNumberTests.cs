using Scheduling.Contracts;
using Xunit;

namespace Scheduling.Tests;

public sealed class TicketDisplayNumberTests
{
    [Theory]
    [InlineData("P101", "InitialConsultation", 1, "P101-001")]
    [InlineData("XN02", "CLS", 27, "XN02-027")]
    [InlineData("P101", "ReturnConsultation", 1, "P101-R001")]
    [InlineData("P101", "CLS", 1000, "P101-1000")]
    [InlineData(null, "CLS", 1, null)]
    [InlineData(" ", "ReturnConsultation", 1, null)]
    public void Formats_only_the_room_and_sequence_without_parsing_the_ticket_key(string? room, string type, int sequence, string? expected) =>
        Assert.Equal(expected, TicketDisplayNumber.Format(room, type, sequence));
}
