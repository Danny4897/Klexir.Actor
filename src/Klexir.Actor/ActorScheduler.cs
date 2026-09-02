using System.Collections.Concurrent;
using Klexir.Actor.Abstractions;

namespace Klexir.Actor;

/// <summary>Schedules one-off or periodic message deliveries to an <see cref="IActorRef{TMessage}"/>.</summary>
public sealed class ActorScheduler : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _scheduled = new();

    /// <summary>Delivers <paramref name="message"/> once, after <paramref name="delay"/>. Returns a cancellable schedule id.</summary>
    public Guid ScheduleOnce<TMessage>(IActorRef<TMessage> target, TMessage message, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(target);

        var scheduleId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _scheduled[scheduleId] = cts;
        _ = RunOnceAsync(scheduleId, target, message, delay, cts.Token);
        return scheduleId;
    }

    /// <summary>Delivers <paramref name="message"/> every <paramref name="interval"/> until cancelled or disposed.</summary>
    public Guid SchedulePeriodic<TMessage>(IActorRef<TMessage> target, TMessage message, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(target);

        var scheduleId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _scheduled[scheduleId] = cts;
        _ = RunPeriodicAsync(scheduleId, target, message, interval, cts.Token);
        return scheduleId;
    }

    /// <summary>Stops a pending or periodic schedule. Returns <see langword="false"/> if the id is unknown or already stopped.</summary>
    public bool Cancel(Guid scheduleId)
    {
        if (!_scheduled.TryRemove(scheduleId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        cts.Dispose();
        return true;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var scheduleId in _scheduled.Keys.ToArray())
        {
            Cancel(scheduleId);
        }

        return ValueTask.CompletedTask;
    }

    private async Task RunOnceAsync<TMessage>(
        Guid scheduleId, IActorRef<TMessage> target, TMessage message, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            await target.TellAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (_scheduled.TryRemove(scheduleId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private async Task RunPeriodicAsync<TMessage>(
        Guid scheduleId, IActorRef<TMessage> target, TMessage message, TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await target.TellAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (_scheduled.TryRemove(scheduleId, out var cts))
            {
                cts.Dispose();
            }
        }
    }
}
