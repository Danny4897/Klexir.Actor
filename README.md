# Klexir.Actor

[![CI](https://github.com/Danny4897/Klexir.Actor/actions/workflows/ci.yml/badge.svg)](https://github.com/Danny4897/Klexir.Actor/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

`Channel<T>`-backed actor primitives for Klexir: serialized state transitions without locks, tell/ask messaging, supervision, scheduling, and domain-event publishing on state change.

> **Status: private research repo, not published to NuGet.** Reference the project directly until/unless it's published.

---

## Quick example

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

---

## What's in the box

| Capability | API | Notes |
|---|---|---|
| Mailbox | `InMemoryActorRef<TMessage,TState>` | `Channel<T>`-backed, single reader, one message at a time |
| Registry | `ActorRegistry` | Create-or-get by string id, type-checked on lookup |
| Lifecycle | `Actor.PreStartAsync` / `PostStopAsync` | Run once before the first message / once when the mailbox stops |
| Supervision | `SupervisionOptions`, `Actor.RecoverAsync` | OneForOne restart budget; mailbox stops once exhausted |
| Tell/ask | `TellAsync` / `AskAsync` | Ask returns the state produced by that message, or throws (failure, restart exhaustion, timeout) |
| Scheduling | `ActorScheduler` | Deliver a message once after a delay, or repeatedly on an interval |
| Domain events | `Actor.ExtractDomainEvents`, `IDomainEventPublisher` | Publish events on state change — bring your own bus (see below) |

## Wiring to Klexir.EventFlow

`IDomainEventPublisher` is Actor's own minimal, `object`-based contract — this repo doesn't depend on `Klexir.EventFlow` (the two aren't on a shared package feed, and a cross-repo `ProjectReference` would break CI). Bridge them yourself in the app that references both: wrap each domain event in an `IEvent`-conforming envelope and forward it.

```csharp
// In the consuming application, which references both Klexir.Actor and Klexir.EventFlow:
public sealed record BalanceChangedEvent(Guid EventId, DateTimeOffset OccurredAt, decimal Old, decimal New) : IEvent;

public sealed class EventFlowPublisher(IEventBus bus) : IDomainEventPublisher
{
    public ValueTask PublishAsync(object domainEvent, CancellationToken ct) =>
        domainEvent switch
        {
            BalanceChanged e => bus.PublishAsync(new BalanceChangedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, e.Old, e.New), ct),
            _ => ValueTask.CompletedTask,
        };
}
```

## Not there yet

- Distributed actors / location transparency (explicitly future work in the original study plan)

## Requirements

.NET 8 SDK. No external dependencies (plain-BCL, predates the ecosystem's MonadicSharp adoption).
