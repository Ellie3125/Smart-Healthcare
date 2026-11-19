using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Holds;
using Shared.Infrastructure.Outbox;
using SmartHealthcare.Worker;
using Xunit;

namespace ArchitectureTests;

public class WorkerHostTests
{
    // Development bật ValidateOnBuild: service đăng ký trong Worker mà thiếu phụ thuộc thì host không dựng được (FIX-014).
    // Chỉ dựng DI, không khởi động hosted service nên không mở kết nối DB.
    [Fact]
    public void Worker_host_builds_in_Development_and_resolves_its_jobs()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Services.AddWorkerServices(builder.Configuration, "Data Source=unused;Initial Catalog=unused");

        using var host = builder.Build();

        using var scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<HoldService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CheckInDeadlineJob>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OutboxDispatcher>());
        Assert.NotNull(host.Services.GetRequiredService<IRecurringJobManager>());
        Assert.Single(host.Services.GetServices<IHostedService>().OfType<OutboxDispatchLoop>());

        // Mỗi sự kiện Outbox Worker nhận phải resolve được đủ handler (factory chỉ chạy khi dispatch, ValidateOnBuild không bắt được).
        Type[] events = [typeof(Billing.Contracts.PaymentVerified), typeof(Scheduling.Contracts.AppointmentConfirmed),
            typeof(Scheduling.Contracts.AppointmentCancelled), typeof(Scheduling.Contracts.TicketCheckedIn), typeof(Scheduling.Contracts.QueueCalled),
            typeof(Scheduling.Contracts.ClsTicketCancelled), typeof(Clinical.Contracts.ResultPublished), typeof(Scheduling.Contracts.ReturnTicketActivated),
            typeof(Scheduling.Contracts.AppointmentDoctorChanged)];
        Assert.Equal(events.Length, scope.ServiceProvider.GetServices<OutboxEventRegistration>().Count());
        foreach (var eventType in events)
        {
            var handlerType = typeof(Shared.Abstractions.IIntegrationEventHandler<>).MakeGenericType(eventType);
            foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
                Assert.NotNull(handler);
        }
    }

    // Một lượt lỗi (DB tạm mất) không được làm dừng vòng lặp phát Outbox.
    [Fact]
    public async Task Outbox_loop_keeps_running_after_a_failed_round()
    {
        var calls = 0;
        var thirdRound = new TaskCompletionSource();
        var loop = new OutboxDispatchLoop(_ =>
        {
            if (++calls == 3)
                thirdRound.TrySetResult();
            return calls == 1 ? Task.FromException(new InvalidOperationException("db down")) : Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(10), Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxDispatchLoop>.Instance);

        await loop.StartAsync(TestContext.Current.CancellationToken);
        await thirdRound.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await loop.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(calls >= 3);
    }
}
