# Two components read one mDNS socket, and a guard that refused them broke both

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-14. Reviewed by a human before merge.*

**Status: a regression was introduced, pushed to `main`, found before it ran on
any machine, and corrected by serialising the call. The underlying problem it
exposed is older, is not fixed, and is the next piece of work.**

## The regression

`Receive and answer mDNS over IPv6` reworked `MdnsSocket.ReceiveAsync` to read
two sockets, holding a pending receive per family and a reusable buffer per
family across calls. Shared state of that kind is safe only under one caller, so
the method gained a re-entrancy guard that threw `InvalidOperationException`,
with a message naming `MdnsResponder.ServeAsync` as the single intended caller.

**That claim was false.** `PrinterResolver.ResolveAsync` reads the same socket.
`ServiceHost` builds one `MdnsSocket`, hands it to both the responder and the
resolver, and runs `responder.ServeAsync` and `RunRelayAsync` concurrently in
one `Task.WhenAll`. The relay resolves the printer through a callback on every
job; when its cached answer is still fresh that returns without touching the
socket, and when it has expired it queries and loops on `ReceiveAsync` while the
responder is blocked in `ReceiveAsync`.

The failure shape was bad in a specific way. `ServeAsync` catches only
`OperationCanceledException` and `SocketException`, so the responder task would
have died; the relay's resolve callback would have faulted at the same moment.
The service would have started cleanly, answered queries, printed correctly for
as long as the resolver's cache stayed warm, and then lost both discovery and
printing on the first job after the printer's TTL lapsed.

## How it was found, and how it should have been

Found while writing instructions to run the service on `FIOS-STB-01`, by reading
`ServiceHost` to see which log lines to expect and noticing that the resolver and
the responder are started together.

It should have been found earlier, in the design walk that preceded the commit.
That walk included a search for callers of `ReceiveAsync`, and the output listed
`src/SecretPrinter.Resolution/PrinterResolver.cs:213`. The evidence was read and
the conclusion was written anyway. A comment asserting "X is the only caller" was
placed in the same session as a search result showing a second caller.

The lesson is narrower than "check your callers": **a claim of the form "this is
the only caller" is a claim about the whole repository, and this project already
had the tool to check it.** The search was run for a different purpose and its
result was not brought to bear on the assertion being written.

## The older problem the guard exposed

Two components have read the same socket concurrently since the resolver was
written. Before this commit each call allocated its own buffer, so nothing was
corrupted - but the datagram taken was whoever's turn it happened to be:

- The responder is in `ReceiveAsync` essentially always.
- When the resolver queries, the printer's reply may be delivered to the
  responder instead, which parses it, sees `IsResponse`, and discards it.
- The resolver then waits out its timeout and reports that the printer did not
  answer, which is true of what it saw and false about the network.

This is a live defect, not a theoretical one. It is unproven whether it explains
the **two print jobs that reset mid-session with connections forcibly closed**,
recorded as unexplained in the handoff of 2026-09-13 - that fault had no capture
and cannot be re-examined. The shapes are compatible. That is a hypothesis, and
it is recorded here as one.

## What was changed now

`ReceiveAsync` serialises on a `SemaphoreSlim` instead of refusing the second
caller. The waiting caller's own cancellation token governs the wait, so a caller
with a deadline still gives up on time rather than blocking on a socket that may
be quiet.

**This makes concurrent use safe. It does not make it correct.** Whoever holds
the lock takes the next datagram whether or not it is theirs, which is exactly
the behaviour described above. The lock removes a crash and restores the
behaviour that existed before the IPv6 commit. It fixes nothing else, and the
code says so where the lock is declared.

A test was added - `Two callers may receive at once without either being
refused` - which starts two concurrent `ReceiveAsync` calls and asserts both
complete. It deliberately does not assert which caller received which datagram,
because pinning that down would freeze behaviour that the next step removes.

## What happens next, and what must not be concluded

**The resolver should have its own socket.** It speaks IPv4 only, on the printer
interface, and has no need of the socket that serves the client networks.
Separating them removes the datagram stealing along with the contention, and is
a change to `ServiceHost` and `PrinterResolver` of its own. That is the next
commit.

Until then: **do not read a successful print as evidence that this is fine.**
With a warm cache the resolver never touches the socket, so the common path
exercises none of this. The interesting case is a job arriving after the cached
answer has expired.
