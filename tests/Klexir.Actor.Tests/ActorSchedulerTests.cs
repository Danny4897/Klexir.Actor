using FluentAssertions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorSchedulerTests
{
    [Fact]
    public async Task ScheduleOnce_delivers_the_message_once_after_the_delay()
    {
        await using var scheduler = new ActorScheduler();
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0);
        await using (actor)
        {
            scheduler.ScheduleOnce(actor, 5, TimeSpan.FromMilliseconds(30));

            (await actor.GetStateAsync()).Should().Be(0);

            await WaitUntilAsync(async () => await actor.GetStateAsync() == 5);
            await Task.Delay(60);
            (await actor.GetStateAsync()).Should().Be(5);
        }
    }

    [Fact]
    public async Task SchedulePeriodic_delivers_the_message_repeatedly_until_cancelled()
    {
        await using var scheduler = new ActorScheduler();
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0);
        await using (actor)
        {
            var scheduleId = scheduler.SchedulePeriodic(actor, 1, TimeSpan.FromMilliseconds(20));

            await WaitUntilAsync(async () => await actor.GetStateAsync() >= 3);

            scheduler.Cancel(scheduleId).Should().BeTrue();
            var stateAfterCancel = await actor.GetStateAsync();
            await Task.Delay(80);
            (await actor.GetStateAsync()).Should().Be(stateAfterCancel);
        }
    }

    [Fact]
    public async Task DisposeAsync_cancels_all_pending_schedules()
    {
        var scheduler = new ActorScheduler();
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0);
        await using (actor)
        {
            scheduler.SchedulePeriodic(actor, 1, TimeSpan.FromMilliseconds(20));
            await WaitUntilAsync(async () => await actor.GetStateAsync() >= 1);

            await scheduler.DisposeAsync();
            var stateAfterDispose = await actor.GetStateAsync();
            await Task.Delay(80);
            (await actor.GetStateAsync()).Should().Be(stateAfterDispose);
        }
    }

    [Fact]
    public async Task Cancel_returns_false_for_an_unknown_schedule_id()
    {
        await using var scheduler = new ActorScheduler();

        scheduler.Cancel(Guid.NewGuid()).Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class CounterActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
    }
}
