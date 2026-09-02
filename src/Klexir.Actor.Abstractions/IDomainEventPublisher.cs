namespace Klexir.Actor.Abstractions;

/// <summary>
/// Sink for domain events an actor emits on a state change. Kept generic (not typed to any specific event-bus
/// package) so Klexir.Actor never has to depend on Klexir.EventFlow — an application wires this to a real event
/// bus with a small adapter of its own.
/// </summary>
public interface IDomainEventPublisher
{
    ValueTask PublishAsync(object domainEvent, CancellationToken cancellationToken = default);
}
