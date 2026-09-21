# A printer that never answers produces a job that fails silently

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-17. Reviewed by a human before merge.*

*Status updated, and the sections "Seen on hardware, 2026-09-21", "The fix",
"Still open" and "A note on how this was found again" added, by Claude (Anthropic model, Claude Opus 5) at the direction
of Edwin West, 2026-09-21. Reviewed by a human before merge. The original text
below the status line is unchanged.*

**Status (2026-09-21): the predicted log shape was seen on FIOS-STB-01 during a
live WLAN failure, and the defect is fixed in the commit that adds this
paragraph. What the hardware run does and does not establish is set out below.
A second, related path — connections cancelled by a withdrawal — is still
open.**

*Corrected 2026-09-21 by Claude (Anthropic model, Claude Opus 5), at the
direction of Edwin West: the table below first said the connections came from
"the iPad". The device at `192.168.1.152` is an iPhone; no iPad was used. Claude
carried the word over from the previous session's handoff without checking it.
Reviewed by a human before merge.*

*Original status (2026-09-17): found while writing the TLS connection factory,
reasoned from the code and from Microsoft's documentation, NOT observed on
hardware. Not fixed. The new TLS code avoids the same shape;
`TcpConnectionFactory` is unchanged and still has it.*

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

## Seen on hardware, 2026-09-21

FIOS-STB-01, running `1dc116c`, with the Broadcom 802.11n adapter (`Wi-Fi`) on
the printer side. This was not the controlled run the section above proposes;
the printer-side WLAN was failing on its own.

| UTC | Log |
|---|---|
| 19:51:06–08 | the printer answered again; listener reopened; advertisement restored |
| 19:53:52–19:54:12 | ten connections from the iPhone (`192.168.1.152`, ports 52762–52771) each logged `Job: accepted` and `Job: located … (cached, 0s)` |
| 19:54:23 onward | new connections failed and were reported: `adapter 'Wi-Fi' is not up` |
| 19:54:52 | printer declared unreachable; advertisement withdrawn; listener closed |

For those ten connections the log holds **no further line at all**: no
`relaying`, no `failed`, no `completed`. That is exactly the prediction written
above on 2026-09-17.

Why the connect deadline is what ended them, by elimination from the code:

- Each connect began at its `located` line, by 19:54:12, and the configured
  connect deadline is 10 seconds, so every deadline had fired by 19:54:22 —
  thirty seconds before the withdrawal at 19:54:52 could have cancelled
  anything.
- A connect that failed with a `SocketException` is caught and reported. None
  was.
- A connect that succeeded would have gone on to the TLS handshake, whose
  deadline is a `TimeoutException` and is reported, and whose success logs
  `relaying`. Neither appears.

What this does **not** establish:

- It is inference from the code, not a capture. No packet trace was taken, so
  it is not shown that the SYNs went unanswered — only that nothing else in the
  code fits a connection that ends with no line.
- Why the printer did not answer is not known. The adapter still reported up
  and the lookup was served from cache; the WLAN was plainly failing, since it
  reported disconnected about ten seconds later, but nothing here measures the
  link during that window.

## The fix

`TcpConnectionFactory.ConnectAsync` now tells its two cancellations apart. If
the caller's token is cancelled — the service stopping, or the printer being
withdrawn — the cancellation propagates unchanged, as before. Otherwise the
factory's own deadline fired, and it throws a `TimeoutException` naming the
destination and the time allowed, with the original cancellation as its inner
exception. `IppRelay` already catches and reports that as `Could not connect to
the printer at …`, the same way it reports a refused connection. `IppRelay` is
unchanged.

To test the deadline without a network, the factory gained an internal
constructor that replaces only the socket connect.
`SecretPrinter.Proxy.csproj` makes internals visible to
`SecretPrinter.Proxy.Tests` alone. The service uses the public constructor,
which connects exactly as before.

Three tests, in `tests/SecretPrinter.Proxy.Tests/TcpConnectionFactoryTests.cs`:

- the factory's own deadline fails as a `TimeoutException` naming the
  destination, with the cancellation kept as its inner exception;
- a cancellation the caller asked for stays a cancellation, so stopping the
  service or withdrawing the printer is never logged as a printer that failed to
  answer;
- end to end through `IppRelay` with the real factory: a connect that never
  completes produces exactly one reported failure, whose reason says the
  connection was not established in time. Before the fix this test ends with
  `RelayOneAsync` throwing a cancellation and the observer holding only
  `accepted` — the 2026-09-21 log shape.

The tests cannot show that a real unanswered SYN reaches this code as a
cancellation. That rests on Microsoft's documentation, described above, and on the
hardware run.

## Still open

**A withdrawal cancels in-flight connections, and nothing reports them.** When
the printer is withdrawn (`REQ-LIF-006`), `ServiceHost` cancels the relay's
token. A connection still connecting, handshaking or relaying at that moment ends
in a cancellation, which `IppRelay` does not report, for the same reason as
above. That is correct in that the printer did not fail those jobs, but it
leaves them with no final line, which is the same silence this finding is about.
It was not the cause of the 2026-09-21 silence — the timing above rules that
out — and it is not changed by this fix. It needs its own decision: what the
log should say about a job the service itself ended.

## A note on how this was found again

The handoff for 2026-09-21 did not list this finding among the open items, and
the explanation for the silent connections was worked out again from the code
before this file was found. The conclusion matched this document's 2026-09-17
prediction. Open findings should be carried in every handoff until they are
closed.

