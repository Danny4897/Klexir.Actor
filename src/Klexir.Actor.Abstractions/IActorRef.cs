namespace Klexir.Actor.Abstractions;

/// <summary>Addressable mailbox for a single actor.</summary>
public interface IActorRef<in TMessage>
{
    ValueTask TellAsync(TMessage message, CancellationToken cancellationToken = default);
}
