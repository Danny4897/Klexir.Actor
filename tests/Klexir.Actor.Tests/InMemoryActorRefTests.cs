using FluentAssertions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class InMemoryActorRefTests
{
    [Fact]
    public async Task TellAsync_processes_messages_as_serial_state_transitions()
    {
        await using var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0);

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => actor.TellAsync(1).AsTask()));

        await WaitUntilAsync(async () => await actor.GetStateAsync() == 100);
        (await actor.GetStateAsync()).Should().Be(100);
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

        throw new TimeoutException("The actor did not process all messages in time.");
    }

    private sealed class CounterActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
    }
}
