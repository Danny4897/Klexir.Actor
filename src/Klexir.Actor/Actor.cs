namespace Klexir.Actor;

/// <summary>Processes one message at a time and produces the next immutable actor state.</summary>
public abstract class Actor<TMessage, TState>
{
    /// <summary>Runs once before the first message is processed. Default is a no-op passthrough of the initial state.</summary>
    public virtual ValueTask<TState> PreStartAsync(TState initialState, CancellationToken cancellationToken) =>
        ValueTask.FromResult(initialState);

    public abstract ValueTask<TState> ReceiveAsync(TMessage message, TState state, CancellationToken cancellationToken);

    /// <summary>
    /// Runs when <see cref="ReceiveAsync"/> throws and the actor's <c>SupervisionOptions</c> still allow a restart.
    /// Computes the state the actor resumes with. Default discards the failed message and keeps the last known-good state.
    /// </summary>
    public virtual ValueTask<TState> RecoverAsync(TState lastState, TMessage failedMessage, Exception exception, CancellationToken cancellationToken) =>
        ValueTask.FromResult(lastState);

    /// <summary>Runs exactly once when the actor's mailbox stops, whether by disposal or by exhausting its restart budget.</summary>
    public virtual ValueTask PostStopAsync(TState finalState, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
