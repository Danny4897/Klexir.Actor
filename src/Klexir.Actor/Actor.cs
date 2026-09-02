namespace Klexir.Actor;

/// <summary>Processes one message at a time and produces the next immutable actor state.</summary>
public abstract class Actor<TMessage, TState>
{
    public abstract ValueTask<TState> ReceiveAsync(TMessage message, TState state, CancellationToken cancellationToken);
}
