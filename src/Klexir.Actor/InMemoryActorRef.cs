using System.Threading.Channels;
using Klexir.Actor.Abstractions;

namespace Klexir.Actor;

/// <summary>Channel-backed actor mailbox that guarantees one state transition at a time.</summary>
public sealed class InMemoryActorRef<TMessage, TState> : IActorRef<TMessage>, IAsyncDisposable
{
    private readonly Channel<MailboxEnvelope> _mailbox;
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
        _mailbox = Channel.CreateUnbounded<MailboxEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });
        _processing = ProcessAsync(_shutdown.Token);
    }

    public ValueTask TellAsync(TMessage message, CancellationToken cancellationToken = default) =>
        _mailbox.Writer.WriteAsync(new MailboxEnvelope(message, null), cancellationToken);

    /// <summary>Sends <paramref name="message"/> and returns the state produced by processing it, or throws if the handler fails or times out.</summary>
    public async Task<TState> AskAsync(TMessage message, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var reply = new TaskCompletionSource<TState>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _mailbox.Writer.WriteAsync(new MailboxEnvelope(message, reply), cancellationToken).ConfigureAwait(false);

        return timeout is { } timeoutValue
            ? await reply.Task.WaitAsync(timeoutValue, cancellationToken).ConfigureAwait(false)
            : await reply.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

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

        var restarts = 0;
        MailboxEnvelope? current = null;

        try
        {
            await foreach (var envelope in _mailbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                current = envelope;
                try
                {
                    _state = await _actor.ReceiveAsync(envelope.Message, _state, cancellationToken).ConfigureAwait(false);
                    envelope.Reply?.TrySetResult(_state);
                    current = null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    if (restarts >= _supervision.MaxRestarts)
                    {
                        envelope.Reply?.TrySetException(ex);
                        current = null;
                        _mailbox.Writer.TryComplete();
                        break;
                    }

                    restarts++;
                    _state = await _actor.RecoverAsync(_state, envelope.Message, ex, cancellationToken).ConfigureAwait(false);
                    envelope.Reply?.TrySetException(ex);
                    current = null;
                }
            }
        }
        finally
        {
            if (current is { } unfinished)
            {
                unfinished.Reply?.TrySetException(
                    new OperationCanceledException("The actor stopped before this message finished processing.", cancellationToken));
            }

            while (_mailbox.Reader.TryRead(out var pending))
            {
                pending.Reply?.TrySetException(
                    new InvalidOperationException("The actor's mailbox stopped before this message was processed."));
            }

            await _actor.PostStopAsync(_state, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private readonly record struct MailboxEnvelope(TMessage Message, TaskCompletionSource<TState>? Reply);
}
