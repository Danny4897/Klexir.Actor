using FluentAssertions;
using Klexir.Actor.Abstractions;
using Xunit;

namespace Klexir.Actor.Tests;

public sealed class ActorDomainEventTests
{
    [Fact]
    public async Task ReceiveAsync_success_publishes_the_actors_extracted_domain_events()
    {
        var publisher = new RecordingPublisher();
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0, domainEventPublisher: publisher);
        await using (actor)
        {
            await actor.AskAsync(5);

            publisher.Published.Should().Equal(new CounterChanged(0, 5));
        }
    }

    [Fact]
    public async Task Actors_without_a_publisher_configured_still_process_messages_normally()
    {
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0);
        await using (actor)
        {
            var result = await actor.AskAsync(5);

            result.Should().Be(5);
        }
    }

    [Fact]
    public async Task Default_ExtractDomainEvents_publishes_nothing()
    {
        var publisher = new RecordingPublisher();
        var actor = new InMemoryActorRef<int, int>(new PlainActor(), 0, domainEventPublisher: publisher);
        await using (actor)
        {
            await actor.AskAsync(5);

            publisher.Published.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task A_publisher_failure_fails_the_message_like_any_other_handler_error()
    {
        var actor = new InMemoryActorRef<int, int>(new CounterActor(), 0, domainEventPublisher: new ThrowingPublisher());
        await using (actor)
        {
            var act = () => actor.AskAsync(5);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    private sealed record CounterChanged(int OldValue, int NewValue);

    private sealed class CounterActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);

        public override IEnumerable<object> ExtractDomainEvents(int previousState, int newState) =>
            [new CounterChanged(previousState, newState)];
    }

    private sealed class PlainActor : Actor<int, int>
    {
        public override ValueTask<int> ReceiveAsync(int message, int state, CancellationToken cancellationToken) =>
            ValueTask.FromResult(state + message);
    }

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<object> Published { get; } = [];

        public ValueTask PublishAsync(object domainEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(domainEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IDomainEventPublisher
    {
        public ValueTask PublishAsync(object domainEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("publish failed");
    }
}
