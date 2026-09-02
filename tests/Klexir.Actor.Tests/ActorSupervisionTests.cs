using System.Threading.Channels;
using FluentAssertions;
using Klexir.Actor.Abstractions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorSupervisionTests
{
    [Fact]
    public async Task ReceiveAsync_failure_stops_the_mailbox_when_no_restarts_are_allowed()
    {
        var actor = new InMemoryActorRef<int, int>(new ThrowsOnNegativeActor(), 0);
        await using (actor)
        {
            await actor.TellAsync(5);
            await WaitUntilAsync(async () => await actor.GetStateAsync() == 5);

            await actor.TellAsync(-1);

            await WaitForMailboxToCloseAsync(actor);
        }
    }

    [Fact]
    public async Task ReceiveAsync_failure_recovers_and_keeps_processing_when_restarts_are_allowed()
    {
        var actor = new InMemoryActorRef<int, int>(
            new ThrowsOnNegativeActor(),
            0,
            supervision: new SupervisionOptions { MaxRestarts = 1 });
        await using (actor)
        {
            await actor.TellAsync(5);
            await actor.TellAsync(-1);
            await actor.TellAsync(3);

            await WaitUntilAsync(async () => await actor.GetStateAsync() == 8);
            (await actor.GetStateAsync()).Should().Be(8);
        }
    }

    [Fact]
    public async Task RecoverAsync_override_computes_the_state_used_after_a_handled_failure()
    {
        var actor = new InMemoryActorRef<int, int>(
            new ResetToZeroOnFailureActor(),
            0,
            supervision: new SupervisionOptions { MaxRestarts = 1 });
        await using (actor)
        {
            await actor.TellAsync(5);
            await actor.TellAsync(-1);
            await actor.TellAsync(3);

            await WaitUntilAsync(async () => await actor.GetStateAsync() == 3);
            (await actor.GetStateAsync()).Should().Be(3);
        }
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

    private static async Task WaitForMailboxToCloseAsync(InMemoryActorRef<int, int> actor)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await actor.TellAsync(0);
            }
            catch (ChannelClosedException)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The mailbox did not close in time.");
    }

    private sealed class ThrowsOnNegativeActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken)
        {
            if (message < 0)
            {
                throw new InvalidOperationException("negative values are not allowed");
            }

            return ValueTask.FromResult(state + message);
        }
    }

    private sealed class ResetToZeroOnFailureActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken)
        {
            if (message < 0)
            {
                throw new InvalidOperationException("negative values are not allowed");
            }

            return ValueTask.FromResult(state + message);
        }

        public override ValueTask<int> RecoverAsync(int lastState, int failedMessage, Exception exception, CancellationToken cancellationToken) =>
            ValueTask.FromResult(0);
    }
}
