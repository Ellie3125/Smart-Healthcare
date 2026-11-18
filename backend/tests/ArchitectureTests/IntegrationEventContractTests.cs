using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Abstractions;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Xunit;

namespace ArchitectureTests;

// docs/conventions.md §6: tên lớp sự kiện trùng EventType để IIntegrationEventPublisher ánh xạ được (W1-06);
// mỗi sự kiện có CurrentVersion và Version trả đúng hằng đó (MSG-03).
public class IntegrationEventContractTests
{
    [Fact]
    public void Every_event_type_fits_its_storage_column()
    {
        using var db = new SharedDbContext(new DbContextOptionsBuilder<SharedDbContext>().UseSqlServer("Server=model-only;Database=none").Options);
        var max = db.Model.FindEntityType(typeof(OutboxMessage))!.FindProperty(nameof(OutboxMessage.EventType))!.GetMaxLength()!.Value;
        Assert.All(Enum.GetNames<EventType>(), name => Assert.InRange(name.Length, 1, max));
    }

    [Fact]
    public void Clinical_alias_registration_rejects_an_undefined_wire_type()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection()
            .AddOutboxEvent<Clinical.Contracts.ClsResultsPublishedIntegrationEvent>((EventType)int.MaxValue));
    }
    public static TheoryData<string> ContractAssemblies => new() { "Billing.Contracts", "Scheduling.Contracts" };

    [Theory]
    [MemberData(nameof(ContractAssemblies))]
    public void Every_integration_event_has_an_outbox_event_type(string assembly)
    {
        var events = Events(assembly);
        Assert.NotEmpty(events);

        var unknown = events.Where(t => !Enum.IsDefined(typeof(EventType), t.Name)).Select(t => t.Name).ToArray();

        Assert.True(unknown.Length == 0, $"Sự kiện chưa có trong EventType: {string.Join(", ", unknown)}");
    }

    [Theory]
    [MemberData(nameof(ContractAssemblies))]
    public void Every_integration_event_declares_its_version(string assembly)
    {
        foreach (var type in Events(assembly))
        {
            var current = type.GetField("CurrentVersion", BindingFlags.Public | BindingFlags.Static);
            Assert.True(current is { IsLiteral: true } && current.FieldType == typeof(int), $"{type.Name} thiếu public const int CurrentVersion");
            var version = type.GetProperty(nameof(IIntegrationEvent.Version))!;
            Assert.True(version.GetMethod!.IsPublic, $"{type.Name}.Version phải public");
        }
    }

    [Fact]
    public void Scheduling_publishes_the_planned_events()
    {
        Assert.Equal(["AppointmentCancelled", "AppointmentConfirmed", "AppointmentDoctorChanged", "ClsTicketCancelled", "QueueCalled", "ReturnTicketActivated", "TicketCheckedIn"],
            Events("Scheduling.Contracts").Select(t => t.Name).Order().ToArray());
    }

    private static Type[] Events(string assembly) =>
        Assembly.Load(new AssemblyName(assembly)).GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(t))
            .ToArray();
}
