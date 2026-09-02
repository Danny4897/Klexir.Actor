using FluentAssertions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorLifecycleTests
{
    [Fact]
    public async Task PreStartAsync_transforms_initial_state_before_the_first_message_is_processed()
    {
        var actor = new InMemoryActorRef<int, int>(new AddTenOnStartActor(), 0);
        await using (actor)
        {
            await actor.TellAsync(1);
            await WaitUntilAsync(async () => await actor.GetStateAsync() == 11);
            (await actor.GetStateAsync()).Should().Be(11);
        }
    }

    [Fact]
    public async Task PostStopAsync_receives_the_final_state_exactly_once_on_dispose()
    {
        var stopped = new List<int>();
        var actor = new InMemoryActorRef<int, int>(new RecordingStopActor(stopped), 0);

        await actor.TellAsync(5);
        await WaitUntilAsync(async () => await actor.GetStateAsync() == 5);
        await actor.DisposeAsync();

        stopped.Should().Equal(5);
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

    private sealed class AddTenOnStartActor : Actor<int, int>
    {
        public override ValueTask<int> PreStartAsync(int initialState, CancellationToken cancellationToken) =>
            ValueTask.FromResult(initialState + 10);

        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
    }

    private sealed class RecordingStopActor(List<int> stopped) : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);

        public override ValueTask PostStopAsync(int finalState, CancellationToken cancellationToken)
        {
            stopped.Add(finalState);
            return ValueTask.CompletedTask;
        }
    }
}
