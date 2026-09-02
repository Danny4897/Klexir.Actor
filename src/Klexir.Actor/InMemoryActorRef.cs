using System.Threading.Channels;
using Klexir.Actor.Abstractions;

namespace Klexir.Actor;

/// <summary>Channel-backed actor mailbox that guarantees one state transition at a time.</summary>
public sealed class InMemoryActorRef<TMessage, TState> : IActorRef<TMessage>, IAsyncDisposable
{
    private readonly Channel<TMessage> _mailbox;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _processing;
    private readonly Actor<TMessage, TState> _actor;
    private TState _state;

    public InMemoryActorRef(Actor<TMessage, TState> actor, TState initialState)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _state = initialState;
        _mailbox = Channel.CreateUnbounded<TMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });
        _processing = ProcessAsync(_shutdown.Token);
    }

    public ValueTask TellAsync(TMessage message, CancellationToken cancellationToken = default) =>
        _mailbox.Writer.WriteAsync(message, cancellationToken);

    public async ValueTask<TState> GetStateAsync()
    {
        await Task.Yield();
        return _state;
    }

    public async ValueTask DisposeAsync()
    {
        _mailbox.Writer.TryComplete();
        _shutdown.Cancel();

        try
        {
            await _processing.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _mailbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _state = await _actor.ReceiveAsync(message, _state, cancellationToken).ConfigureAwait(false);
        }
    }
}
