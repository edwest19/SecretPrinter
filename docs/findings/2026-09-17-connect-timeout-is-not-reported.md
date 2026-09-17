# A printer that never answers produces a job that fails silently

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-17. Reviewed by a human before merge.*

**Status: found while writing the TLS connection factory, reasoned from the code
and from Microsoft's documentation, NOT observed on hardware. Not fixed. The new
TLS code avoids the same shape; `TcpConnectionFactory` is unchanged and still has
it.**

## What the gap is

`TcpConnectionFactory.ConnectAsync` bounds its connect with a linked
cancellation source:

```csharp
using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
deadline.CancelAfter(timeout);

await client.ConnectAsync(destination, deadline.Token).ConfigureAwait(false);
```

Microsoft's documentation for `TcpClient.ConnectAsync(IPEndPoint,
CancellationToken)` states that when the token is cancelled the operation throws
`OperationCanceledException`, stored into the returned task. So when the timeout
elapses — a printer that is switched off, or on a network that drops the SYN
rather than refusing it — `ConnectAsync` throws `OperationCanceledException`.

`IppRelay.RelayOneAsync` guards that call with:

```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
```

so this exception is not caught there. It leaves `RelayOneAsync` after the
`finally` has disposed the client connection. `IppRelay.RunAsync` started that
job with `Task.Run(...)` and keeps it in a list it prunes on every accept with
`running.RemoveAll(t => t.IsCompleted)`, so the faulted task is dropped before
the `await Task.WhenAll(running)` at shutdown could surface it, and its
exception is never observed.

The result: the client's connection is closed, nothing is printed, and **no
`RelayFailed` is reported and nothing is logged**. An operator sees a job that
did nothing and a log that says nothing.

Two requirements are marked as met on that code path and are not, in this case:

- `REQ-PXY-007` — connections that cannot be established are refused promptly
  **with a logged reason**.
- `REQ-OBS-006` — a failed relayed connection logs a reason naming the peer.

A printer that actively refuses the connection is a different case and does work:
that produces a `SocketException`, which the relay catches and reports. The tests
that cover `REQ-PXY-007` use exactly that case (`FakeConnectionFactory.FailWith =
new SocketException(10061)`), which is why they pass.

## Why this was not fixed in the same change

The commit that found it was adding TLS to the printer, and that is one thing at
a time. The fix is small — translate the factory's own deadline into a
`TimeoutException`, as `TlsConnectionFactory` now does for its handshake — but it
changes how an existing, marked requirement behaves and deserves its own commit
and its own test.

`TlsConnectionFactory` does not repeat the mistake. Its handshake deadline throws
`TimeoutException`, deliberately and with a comment saying why, so a printer that
accepts a connection and then says nothing is reported as a failed job.

## What would settle it

It has not been reproduced. The honest way to confirm it is to point the service
at an address on the printer network with nothing listening — one that drops the
SYN rather than refusing — send a job, and read the log. Predicted before the
run: the job fails, the log shows `Accepting print jobs …` and the per-job
`Job: located …` line, and then **no** `Job failed …` line at all.
