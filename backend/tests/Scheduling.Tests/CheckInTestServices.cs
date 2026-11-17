using System.Reflection;
using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Scheduling.Tests;

internal static class CheckInTestServices
{
    public static IServiceCollection AddCheckInTestServices(this IServiceCollection services) => services
        .AddSingleton<ICheckInStore, Store>().AddSingleton<IUnitOfWork, Uow>()
        .AddSingleton<IIntegrationEventPublisher, Publisher>()
        .AddSingleton(DispatchProxy.Create<IClinicalModule, UnusedContract>())
        .AddSingleton(DispatchProxy.Create<IIdentityPatientsModule, UnusedContract>());

    public sealed class Store : ICheckInStore
    {
        public CheckInAppointment? LastCommand { get; private set; }
        public SchedulingErrorCode? Error { get; set; }
        public Task<CheckInOutcome> CheckInAsync(CheckInAppointment command,
            Func<AppointmentForCheckIn, CancellationToken, Task<Guid>> open, CancellationToken ct)
        {
            LastCommand = command;
            return Task.FromResult(Error is { } error ? new CheckInOutcome(Error: error) : new CheckInOutcome(
                new CheckInResult(command.AppointmentId, Guid.NewGuid(), Guid.NewGuid(), new(2026, 11, 17),
                    DateTime.UtcNow, false, Guid.NewGuid(), "001", 1, Guid.NewGuid()), Guid.NewGuid()));
        }
        public Task<IReadOnlyList<TodayAppointment>> ListTodayAsync(Guid? d, string? n, int skip, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TodayAppointment>>([]);
        public Task<IReadOnlyList<Guid>> FindOverdueQuotasAsync(int take, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> MarkAbsentAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
    public sealed class Uow : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
    public sealed class Publisher : IIntegrationEventPublisher
    {
        public Task PublishAsync<T>(T e, Guid id, CancellationToken ct) where T : IIntegrationEvent => Task.CompletedTask;
    }
    public class UnusedContract : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Contract không được gọi trong test này: {targetMethod?.Name}");
    }
}
