using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only interceptor used to force a deterministic check-then-insert race.
/// When armed, every SaveChanges call that is about to INSERT a new <see cref="User"/>
/// is held until the configured number of participants have all reached that point.
/// This guarantees every concurrent request has already performed its
/// "does this email exist?" lookup (and seen no row) before any of them writes,
/// so the database unique constraint is what actually resolves the conflict.
/// When not armed, it is a no-op.
/// </summary>
public sealed class UserInsertBarrierInterceptor : SaveChangesInterceptor
{
    private volatile ArmedBarrier? _current;

    /// <summary>
    /// Arms the barrier for <paramref name="participants"/> concurrent user inserts.
    /// Dispose the returned handle to disarm it.
    /// </summary>
    public ArmedBarrier Arm(int participants, TimeSpan timeout)
    {
        var barrier = new ArmedBarrier(this, participants, timeout);
        if (Interlocked.CompareExchange(ref _current, barrier, null) is not null)
        {
            throw new InvalidOperationException("The user insert barrier is already armed.");
        }

        return barrier;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var barrier = _current;

        if (barrier is not null &&
            eventData.Context is not null &&
            eventData.Context.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Added))
        {
            await barrier.SignalAndWaitAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Disarm(ArmedBarrier barrier) =>
        Interlocked.CompareExchange(ref _current, null, barrier);

    public sealed class ArmedBarrier : IDisposable
    {
        private readonly UserInsertBarrierInterceptor _owner;
        private readonly int _participants;
        private readonly TimeSpan _timeout;
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        internal ArmedBarrier(UserInsertBarrierInterceptor owner, int participants, TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(participants, 2);
            _owner = owner;
            _participants = participants;
            _timeout = timeout;
        }

        /// <summary>
        /// Number of requests that reached the INSERT point while the barrier was armed.
        /// </summary>
        public int ArrivedCount => Volatile.Read(ref _arrived);

        internal async Task SignalAndWaitAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _arrived) >= _participants)
            {
                _allArrived.TrySetResult();
            }

            try
            {
                await _allArrived.Task.WaitAsync(_timeout, ct);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    $"Only {ArrivedCount} of {_participants} concurrent requests reached the user INSERT " +
                    $"within {_timeout}. The race was not reproduced.", ex);
            }
        }

        public void Dispose()
        {
            // Release anyone still waiting so a failing test never hangs the suite.
            _allArrived.TrySetResult();
            _owner.Disarm(this);
        }
    }
}