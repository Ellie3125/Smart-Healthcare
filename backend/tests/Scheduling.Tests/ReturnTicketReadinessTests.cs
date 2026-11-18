using Clinical.Contracts;
using Scheduling.Application.Queues;
using Xunit;

namespace Scheduling.Tests;

public sealed class ReturnTicketReadinessTests
{
    [Theory]
    [InlineData("Confirmed", "Completed", "Published", true)]
    [InlineData("Completed", "ServiceFinished", "Published", true)]
    [InlineData("Draft", "Completed", "Published", false)]
    [InlineData("Cancelled", "Completed", "Published", false)]
    [InlineData("Confirmed", "ServiceFinished", "Draft", false)]
    [InlineData("Confirmed", "InProgress", "Published", false)]
    [InlineData("Confirmed", "Cancelled", "Published", false)]
    public void Only_published_mandatory_results_in_the_current_order_are_ready(string batch, string itemStatus, string resultStatus, bool expected)
    {
        var encounter = Guid.NewGuid(); var order = Guid.NewGuid(); var item = Guid.NewGuid();
        var line = new ServiceOrderItemDto(item, order, Guid.NewGuid(), "Test", 100, itemStatus, true, null,
            new(Guid.NewGuid(), item, "", "", resultStatus, null, null, null, null, DateTime.UtcNow));
        var round = new ServiceOrderDto(order, encounter, 1, DateTime.UtcNow, Guid.NewGuid(), batch, false, [line]);
        Assert.Equal(expected ? ReturnTicketReadiness.Ready : ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order, [round]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(Guid.NewGuid(), order, [round]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, Guid.NewGuid(), [round]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order, [round with { Items = [] }]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order, [round with { Items = [line with { Result = null }] }]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order,
            [round with { Items = [line with { Result = line.Result! with { PublishedAtUtc = null } }] }]));
        if (batch is "Confirmed" or "Completed")
            Assert.Equal(ReturnTicketReadiness.Superseded, ReturnTicketRules.Check(encounter, order, [round, round with { Id = Guid.NewGuid(), OrderRoundNumber = 2 }]));
    }

    [Fact]
    public void Batch_without_mandatory_items_needs_every_active_item_published()
    {
        var encounter = Guid.NewGuid(); var order = Guid.NewGuid();
        ServiceOrderItemDto Line(string status, string? result) { var id = Guid.NewGuid();
            return new(id, order, Guid.NewGuid(), "Tùy chọn", 100, status, false, null,
                result is null ? null : new(Guid.NewGuid(), id, "", "", result, null, null, null, null, DateTime.UtcNow)); }
        ServiceOrderDto Round(params ServiceOrderItemDto[] items) => new(order, encounter, 1, DateTime.UtcNow, Guid.NewGuid(), "Confirmed", false, items);

        Assert.Equal(ReturnTicketReadiness.Ready, ReturnTicketRules.Check(encounter, order, [Round(Line("Completed", "Published"))]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order, [Round(Line("Completed", "Published"), Line("ServiceFinished", "Draft"))]));
        // Mục đã hủy không chặn; đợt toàn mục hủy không có gì để chờ thì không kích hoạt.
        Assert.Equal(ReturnTicketReadiness.Ready, ReturnTicketRules.Check(encounter, order, [Round(Line("Completed", "Published"), Line("Cancelled", null))]));
        Assert.Equal(ReturnTicketReadiness.NotReady, ReturnTicketRules.Check(encounter, order, [Round(Line("Cancelled", null))]));
        // Có mục bắt buộc thì mục tùy chọn chưa xong không chặn.
        var mandatory = Line("Completed", "Published") with { IsMandatory = true };
        Assert.Equal(ReturnTicketReadiness.Ready, ReturnTicketRules.Check(encounter, order, [Round(mandatory, Line("Ordered", null))]));
    }
}
