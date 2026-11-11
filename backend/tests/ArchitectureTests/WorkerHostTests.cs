using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OutboxDispatcher>());
        Assert.NotNull(host.Services.GetRequiredService<IRecurringJobManager>());
    }
}
