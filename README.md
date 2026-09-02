# Klexir.Actor

Channel-backed actor primitives for Klexir. Each actor mailbox guarantees serialized state transitions. `ActorRegistry` creates and tracks actors by id, `Actor<TMessage,TState>` exposes `PreStartAsync`/`PostStopAsync` lifecycle hooks, and `SupervisionOptions` drives a OneForOne restart strategy (`RecoverAsync` computes the resume state after a handled failure; the mailbox stops once the restart budget is exhausted). Scheduler/timers, tell/ask, EventFlow integration and distributed actors are planned next.
