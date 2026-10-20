# A connection accepted as the relay stopped was left open

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-20. Reviewed by a human before merge.*

**Status: found by reading the code on 2026-10-18; never seen on a machine.
Corrected in the change this finding comes with, and measured against the code
by a test that fails without the correction; not run on hardware, because the
moment it needs cannot be made to happen on demand there. Every release so
far, 0.1.0, 0.2.0 and 0.3.0, carries the fault: it has been in
`IppRelay.RunAsync` since the first commit. A client connection the listener
accepted in the moment the service stopped, or withdrew the printer
(`REQ-LIF-006`), could be left open, with nothing reading it and nothing in the
service closing it, and nothing in the log about it. `REQ-LIF-002` says the
service terminates in-flight relays on shutdown; this one was never started,
so never terminated.**

No address, device name or network name from the household's networks appears
in this finding. The addresses in the test are made up.

## What the code did

`IppRelay.RunAsync` accepts connections in a loop and starts a relay for each:

```csharp
client = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
...
running.RemoveAll(t => t.IsCompleted);
running.Add(Task.Run(() => RelayOneAsync(client, cancellationToken), cancellationToken));
```

The client connection is closed in one place only: the `finally` of
`RelayOneAsync`. And `Task.Run` was given the token. Microsoft's page for this
overload of `Task.Run` (`Run(Func<Task>, CancellationToken)`, Microsoft Learn,
read 2026-10-20) describes that token as "A cancellation token that can be used
to cancel the work if it has not yet started", and the test below measured
that the relay does not run when it is cancelled first. So when the token was
cancelled after
`AcceptAsync` had handed over a connection and before the thread pool began
`RelayOneAsync`, that relay never ran, the connection was never closed, and
`RelayOneAsync` never told the observer it was accepted, so the log held no
line for it. The loop then ended on the cancelled token, and the wait for the
running relays ended with the cancelled task.

The service cancels that token (`ServiceHost.AcceptUntilWithdrawnAsync`) when
it stops; when it withdraws the printer because the printer stopped answering
(`REQ-LIF-006`), which on FIOS-STB-01 happens several times a day; and when
one of its accept loops ends by itself, so that all of them stop together. At a stop the
process ends, and Windows closes what the process held. At a withdrawal the
process goes on; nothing in the service's code closed the connection, and the
client at the other end was left with a connection that nothing on this side
would read or close.

How often a connection arrived in that moment is not known. It could not be
read from the log, because such a connection left no line in it.

## The correction

`Task.Run` is no longer given the token:

```csharp
running.Add(Task.Run(() => RelayOneAsync(client, cancellationToken)));
```

So every relay starts, and every accepted connection reaches the `finally` that
closes it. `RelayOneAsync` is still given the token. A relay started after the
cancellation logs `Job: accepted from` and the client, checks its address as
every relay does (`REQ-SEC-012`) and, if the address is permitted, asks the
resolver for the printer's address with a cancelled token. The service's
resolver (`PrinterResolver.ResolveAsync`) checks its token before anything else
and throws, so the relay ends there, opens nothing to the printer, and closes
the client's connection. As for any relay cut by a stop or a withdrawal,
nothing is logged for its end (the note on `REQ-PXY-009`); for this one, the
log holds `Job: accepted` and no `Job: relaying`.

`RunAsync` now carries a `REQ-LIF-002` marker saying so. No other code changed.

## The test

`A connection accepted as the relay is stopped is still closed`
(`IppRelayTests`, marked `REQ-LIF-002`) uses a listener written for it,
`CancelsThenAcceptsListener`, which cancels the token and then hands over one
connection, in that order. That puts the connection in the moment described
above every time, without depending on timing. The resolver it gives the relay
checks its token first, as the service's does. The test asserts that
`RunAsync` ends, that the connection was closed, and that no connection to the
printer was attempted.

- **Against the code before the correction** it failed, on the second
  assertion only: the connection was not closed.
- **With the correction** it passes, and passed 20 runs of the suite in a row.
- **Three changes made on purpose to the corrected code,** each run against
  the suite: giving `Task.Run` the token again (the new test fails); giving
  `RelayOneAsync` no token (the new test fails: the relay goes on to the
  printer and waits on the client); and removing the close in `RelayOneAsync`'s
  `finally` (the new test and two older ones fail).

## What this does not show

- **That it ever happened** on FIOS-STB-01 or anywhere else: a connection
  caught in that moment left no line in the log.
- **A run on hardware.** The moment lasts from the accept to the start of the
  relay on the thread pool; it cannot be made to happen on demand with a real
  iPhone.
- **What happens with a resolver that ignores its token.** `IppRelay` takes
  the resolver as a delegate. With one that ignores a cancelled token, the
  relay would go on to the connection factory, which is given the same token;
  what the service's factory does then was not tested. The service's own
  resolver checks the token first, by reading its code.

## How it was found and checked

Read in `IppRelay.cs` on 2026-10-18 while correcting the comment above the wait
for running relays (commit `3444b06`), and recorded then as an item to settle.
On 2026-10-20 it was read again, the test was written first and run against the
code before the correction, then the correction was made, and every suite was
run in Claude's workspace (Linux, .NET SDK 10.0.113) and then on the
development machine by Edwin West before the commit.
