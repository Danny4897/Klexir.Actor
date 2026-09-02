# Quick example

```csharp
public sealed record Deposit(decimal Amount);
public sealed record BalanceChanged(decimal Old, decimal New);

public sealed class AccountActor : Actor<Deposit, decimal>
{
    public override ValueTask<decimal> ReceiveAsync(Deposit message, decimal balance, CancellationToken ct) =>
        ValueTask.FromResult(balance + message.Amount);

    // Runs after every successful ReceiveAsync — return events to publish.
    public override IEnumerable<object> ExtractDomainEvents(decimal previous, decimal next) =>
        [new BalanceChanged(previous, next)];
}

await using var account = new InMemoryActorRef<Deposit, decimal>(
    new AccountActor(),
    initialState: 0m,
    supervision: new SupervisionOptions { MaxRestarts = 3 });

await account.TellAsync(new Deposit(100m));                 // fire-and-forget
decimal balance = await account.AskAsync(new Deposit(50m)); // 150m — waits for the result
```

One mailbox, one message processed at a time — no lock needed to touch `balance` inside `ReceiveAsync`.

See the [full README](https://github.com/Danny4897/Klexir.Actor#readme) on GitHub for supervision, scheduling, and the current gaps.
