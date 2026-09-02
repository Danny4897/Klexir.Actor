using System.Collections.Concurrent;
using Klexir.Actor.Abstractions;

namespace Klexir.Actor;

/// <summary>Creates and tracks actors by id, guaranteeing at most one instance per id.</summary>
public sealed class ActorRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object> _actors = new();

    /// <summary>
    /// Returns the actor already registered under <paramref name="actorId"/>, or creates one via
    /// <paramref name="actorFactory"/> and registers it. Throws if the id is already bound to a different
    /// message/state type.
    /// </summary>
    public IActorRef<TMessage> GetOrCreate<TMessage, TState>(
        string actorId,
        Func<Actor<TMessage, TState>> actorFactory,
        TState initialState,
        SupervisionOptions? supervision = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorId);
        ArgumentNullException.ThrowIfNull(actorFactory);

        var entry = _actors.GetOrAdd(
            actorId,
            _ => new InMemoryActorRef<TMessage, TState>(actorFactory(), initialState, supervision));

        if (entry is not IActorRef<TMessage> actorRef)
        {
            throw new InvalidOperationException(
                $"Actor '{actorId}' is already registered with an incompatible message type.");
        }

        return actorRef;
    }

    /// <summary>Looks up a previously created actor by id without creating one.</summary>
    public bool TryGet<TMessage>(string actorId, out IActorRef<TMessage>? actorRef)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorId);

        if (_actors.TryGetValue(actorId, out var entry) && entry is IActorRef<TMessage> typed)
        {
            actorRef = typed;
            return true;
        }

        actorRef = null;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var entry in _actors.Values)
        {
            if (entry is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
