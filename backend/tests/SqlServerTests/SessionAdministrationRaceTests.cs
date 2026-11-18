using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.CheckIn;
using Scheduling.Application.Confirmations;
using Scheduling.Contracts;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

public sealed partial class SessionAdministrationTests
{
    [Fact]
    public async Task Check_in_waiter_uses_the_stable_quota_id_after_admin_replaces_the_doctor()
    {
        await using var f = await Fixture.CreateAsync();
        var pause = new PauseQuotaLock();
        var cs = TestDatabase.RequiredConnectionString();
        await using var waitingServices = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddDbContext<SchedulingDbContext>(o => o.AddInterceptors(pause)).AddUnitOfWorkParticipant<SchedulingDbContext>()
            .AddSingleton<IDbClock>(f.Clock).BuildServiceProvider();
        var checkIn = Task.Run(async () =>
        {
            await using var scope = waitingServices.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t =>
                scope.ServiceProvider.GetRequiredService<ICheckInStore>().CheckInAsync(new(f.AppointmentId, Guid.NewGuid(), null),
                    (_, _) => Task.FromResult(f.EncounterId), t), Ct);
        }, Ct);
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            await f.ReplaceAsync(f.Directory.Replacement);
        }
        finally { pause.Release.TrySetResult(); }
        var outcome = await checkIn;
        Assert.Equal(f.Directory.Replacement, outcome.Result!.DoctorId);
    }

    [Fact]
    public async Task Held_booking_keeps_its_priced_doctor_while_confirmation_uses_the_replacement_doctor()
    {
        await using var f = await Fixture.CreateAsync();
        await f.ReplaceAsync(f.Directory.Replacement);
        await using var scope = f.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IConfirmationStore>();
        var candidate = await store.FindCandidateAsync(f.HeldAttemptId, Ct);
        Assert.Equal(f.Directory.Original, candidate!.DoctorId);
        var result = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => store.ConfirmAsync(candidate.ReservationId, 150_000m, t), Ct);
        Assert.Equal(f.Directory.Replacement, Assert.IsType<ConfirmResult.Confirmed>(result).DoctorId);
    }

    private sealed class PauseQuotaLock : DbCommandInterceptor
    {
        private int _paused;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DoctorDayQuota", StringComparison.Ordinal) && command.CommandText.Contains("UPDLOCK", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _paused, 1) == 0)
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
