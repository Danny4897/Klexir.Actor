using FluentAssertions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorRegistryTests
{
    [Fact]
    public async Task GetOrCreate_returns_the_same_actor_ref_for_repeated_calls_with_the_same_id()
    {
        await using var registry = new ActorRegistry();

        var first = registry.GetOrCreate<int, int>("counter", () => new CounterActor(), 0);
        var second = registry.GetOrCreate<int, int>("counter", () => new CounterActor(), 0);

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task TryGet_finds_a_previously_created_actor_by_id()
    {
        await using var registry = new ActorRegistry();
        registry.GetOrCreate<int, int>("counter", () => new CounterActor(), 0);

        registry.TryGet<int>("counter", out var actorRef).Should().BeTrue();
        actorRef.Should().NotBeNull();
    }

    [Fact]
    public async Task TryGet_returns_false_for_an_unknown_id()
    {
        await using var registry = new ActorRegistry();

        registry.TryGet<int>("missing", out var actorRef).Should().BeFalse();
        actorRef.Should().BeNull();
    }

    private sealed class CounterActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
    }
}
