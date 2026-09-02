namespace Klexir.Actor.Abstractions;

/// <summary>
/// OneForOne restart policy applied when an actor's <c>ReceiveAsync</c> throws.
/// Default (<see cref="MaxRestarts"/> = 0) preserves the non-supervised baseline: the mailbox stops on first failure.
/// </summary>
public sealed record SupervisionOptions
{
    /// <summary>Number of failures the actor may recover from before its mailbox stops accepting new messages.</summary>
    public int MaxRestarts { get; init; }
}
