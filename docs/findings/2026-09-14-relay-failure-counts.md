# A requirement was marked covered while half of what it requires was missing

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-14. Reviewed by a human before merge.*

**Status: corrected in the commit that carries this document. The marker stayed
on throughout; this document, not the commit history, is what records that it
was wrong.**

## What the marker claimed

REQ-PXY-009 reads:

> Log entries about a relayed connection record endpoints, byte counts, and
> timing — never job content.

`IppRelay.RelayOneAsync` has carried a `[Requirement("REQ-PXY-009")]` marker
since the relay was written, and three tests carry the same identifier. SpecCheck
therefore reported the requirement as `OK`, which under this project's rules
means an implementation marker and a passing test both exist.

What actually existed:

```csharp
void RelayCompleted(
    EndPoint? client, IPEndPoint printer, long bytesToPrinter, long bytesToClient, TimeSpan duration);

void RelayFailed(EndPoint? client, string reason);
```

A completed relay reported counts and timing. A failed relay reported neither,
and no test asked it to. `RelayOneAsync` held both numbers at the point it called
`RelayFailed` — it returned them to its caller in `RelayOutcome` on the very next
line — and did not pass them.

A failed relay is a relayed connection. The requirement does not say "a
successful one", and there is no reading of its text under which the failure path
is out of scope. The marker asserted more than was true.

## What that cost

It was not a theoretical gap. On 2026-09-14, twenty-two relayed connections to
the Epson ET-3760 failed on `FIOS-STB-01`, every one of them reporting `Relay
ended early while reading from the printer`
(`docs/findings/2026-09-14-ipv6-is-the-only-transport.md`). Two explanations fit
that log equally well:

- the connections opened, carried nothing, and the printer reaped them as idle;
- the connections carried a job the printer then rejected part-way through.

These are different faults with different fixes, and the log could not tell them
apart, because the one number that separates them was the number not being
reported. A session was spent proposing and discarding hypotheses that a byte
count would have settled.

## Why it was not caught

The three tests marked REQ-PXY-009 — `Counts_are_reported`,
`Completion_is_reported_when_one_side_closes` and `Counts_survive_cancellation` —
all drive a relay that ends cleanly. Every one of them passes without the failure
path reporting anything at all. The marker was placed against the shape of the
requirement rather than against a reading of each path the code can take through
it, and the tests that were then written extended the same assumption rather than
testing it.

SpecCheck cannot catch this, and says so in its own output: it finds omissions,
not dishonest or careless markers. A green run means a marker and a test exist.
It is not a claim that the marked code does what the requirement says. This is
the failure mode that disclaimer exists for, and it happened anyway.

## What changed

`IRelayObserver.RelayFailed` now takes the same counts and duration as
`RelayCompleted`:

```csharp
void RelayFailed(
    EndPoint? client, string reason, long bytesToPrinter, long bytesToClient, TimeSpan duration);
```

Both implementers were updated: `RelayLogger` in `ServiceHost`, which prints the
numbers in the same form as the completed line, and `RecordingObserver` in the
Proxy tests. Two tests were added — one asserting that a relay which carried
bytes before the printer stopped accepting them reports those bytes, one
asserting that a failure occurring before any byte could move reports zero rather
than nothing.

Three decisions worth stating, since each was taken deliberately:

**Zero counts on the pre-relay failures are a measurement.** A failed resolve or
an unreachable printer reports `0, 0` because nothing was carried, and zero is
the honest count of nothing. The duration on those paths is the time the attempt
took, read once from the stopwatch and given to both the observer and the caller
rather than read twice.

**One duration is not a measurement.** A failed `AcceptAsync` reports
`TimeSpan.Zero` because no connection existed to time. That zero means "not
measured" while every other zero in these parameters means "measured, and none".
It is the one place they differ, it is documented on the interface, and it is not
covered by a test: a failed accept is not a relayed connection, so REQ-PXY-009
does not reach it.

**The parameters are spelled out rather than bundled.** Passing a record holding
the three values would read better and would weaken REQ-OBS-004, whose test scans
this interface's parameter types for anything able to carry content. That scan
does not follow into a record's properties. Primitives keep every type an
implementation is handed visible to it.

## The marker

Under this project's rule — a marker goes on only when the whole requirement is
met — the strictly correct action was to remove the REQ-PXY-009 markers in one
commit, let SpecCheck report six binding gaps, and restore them with the fix.
Edwin chose instead to leave them on and land the correction, the tests and this
document together, on the grounds that this document is the durable record and a
one-commit red state adds little to it. The alternative is written down here so
that the choice is visible rather than implicit.

## The lesson

A marker is a claim about every path through the code it sits on, not about the
happy one. The check that was missing is cheap: for each requirement being
marked, name the paths the method can take and say what the requirement demands
of each. Both of the observability changes made this session were about telling
two failures apart. The gap that made that necessary was a requirement already
marked as met.
