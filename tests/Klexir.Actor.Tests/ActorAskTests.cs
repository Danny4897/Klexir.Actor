using FluentAssertions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorAskTests
{
    [Fact]
    public async Task AskAsync_returns_the_state_produced_by_processing_the_message()
    {
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 10);
        await using (actor)
        {
            var result = await actor.AskAsync(5);

            result.Should().Be(15);
        }
    }

    [Fact]
    public async Task AskAsync_propagates_the_handler_exception_when_no_restart_is_allowed()
    {
        var actor = new InMemoryActorRef<int, int>(new ThrowsOnNegativeActor(), 0);
        await using (actor)
        {
            var act = () => actor.AskAsync(-1);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task AskAsync_times_out_when_the_handler_does_not_complete_in_time()
    {
        var actor = new InMemoryActorRef<int, int>(new BlocksUntilCancelledActor(), 0);
        await using (actor)
        {
            var act = () => actor.AskAsync(1, timeout: TimeSpan.FromMilliseconds(50));

            await act.Should().ThrowAsync<TimeoutException>();
        }
    }

    [Fact]
    public async Task AskAsync_faults_pending_requests_when_the_actor_is_disposed_before_they_are_processed()
    {
        var actor = new InMemoryActorRef<int, int>(new BlocksUntilCancelledActor(), 0);

        var blockingAsk = actor.AskAsync(1);
        var queuedAsk = actor.AskAsync(2);

        await actor.DisposeAsync();

        (await Record.ExceptionAsync(() => blockingAsk)).Should().NotBeNull();
        (await Record.ExceptionAsync(() => queuedAsk)).Should().NotBeNull();
    }

    private sealed class CounterActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
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

    private sealed class BlocksUntilCancelledActor : Actor<int, int>
    {
        public override async ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return state;
        }
    }
}
