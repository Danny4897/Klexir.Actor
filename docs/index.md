---
layout: home

hero:
  name: "Klexir.Actor"
  text: "Actor model primitives"
  tagline: Channel<T>-backed actors for Klexir — serialized state transitions without locks, tell/ask messaging, supervision, and domain-event publishing on state change.
  actions:
    - theme: brand
      text: Quick example
      link: /guide
    - theme: alt
      text: Full README on GitHub
      link: https://github.com/Danny4897/Klexir.Actor
    - theme: alt
      text: Klexir Ecosystem
      link: https://danny4897.github.io/MonadicSharp/ecosystem

features:
  - title: No locks, one mailbox
    details: One message processed at a time per actor — nothing inside ReceiveAsync needs a lock to touch state.
  - title: tell vs ask
    details: Fire-and-forget with TellAsync, or wait for the result with AskAsync — same actor, same mailbox.
  - title: Part of the Klexir Ecosystem
    details: One of 7 experimental .NET repos exploring systems-programming concepts — see the full ecosystem on MonadicSharp's docs.
---
