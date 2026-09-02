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
    private readonly SupervisionOptions _supervision;
    private TState _state;

    public InMemoryActorRef(Actor<TMessage, TState> actor, TState initialState, SupervisionOptions? supervision = null)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _state = initialState;
        _supervision = supervision ?? new SupervisionOptions();
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
        _state = await _actor.PreStartAsync(_state, cancellationToken).ConfigureAwait(false);

        try
        {
            var restarts = 0;

            await foreach (var message in _mailbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    _state = await _actor.ReceiveAsync(message, _state, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    if (restarts >= _supervision.MaxRestarts)
                    {
                        _mailbox.Writer.TryComplete();
                        break;
                    }

                    restarts++;
                    _state = await _actor.RecoverAsync(_state, message, ex, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await _actor.PostStopAsync(_state, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
