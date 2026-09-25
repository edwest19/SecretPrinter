# REQ-RES-008 was marked met, but two of its clauses were never built

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-25. Reviewed by a human before merge.*

**Status: found by reading the code, not observed on hardware. One of the two
clauses is built in the commit that adds this finding; the other is not yet.
Until it is, the `REQ-RES-008` markers in the code claim more than the code
does.**

## What the requirement says

`REQ-RES-008` in the README ends its description of the reachability schedule
with two clauses about print jobs:

> A resolution performed because a job arrived resets the schedule; concurrent
> jobs share one in-flight query rather than issuing one apiece.

The requirement carries `[Requirement("REQ-RES-008", …)]` markers on
`PrinterReachability` (the class and `RecordSilence`) and on
`PrinterWatch.WatchAsync`. A marker is placed only when the whole requirement is
met.

## What the code did

**A job's lookup never reached the schedule.** `PrinterReachability` holds the
schedule. It learns of lookups only through `RecordAnswer` and `RecordSilence`,
and the only caller of either is `PrinterWatch.AskAsync`, which runs the watch's
own queries.
A print job finds the printer through `ServiceHost.LocateConnectionAsync`, which
calls `PrinterResolver.ResolveAsync` directly. Its answer, or its failure, goes
no further.

`PrinterReachability`'s header said the opposite:

> A lookup performed because a print job arrived is a demand-driven query
> rather than part of either stream. Its outcome is recorded here just the
> same, and resets the schedule.

That sentence is kept here as it was. It is corrected in the commit that makes
a job's lookup reach the schedule.

**Jobs shared a query only when it was answered.** `ResolveAsync` serialised
lookups with a semaphore. A lookup that arrived while another was running
waited for it. If that query was answered, the waiting lookup found the answer
in the cache and asked nothing. If it went unanswered, the waiting lookup asked
the same question again and waited out its own timeout. Three jobs arriving
together at a printer that did not answer therefore waited for three timeouts
in turn. That is the queue the
[2026-09-18 finding](2026-09-18-the-printer-side-interface-goes-away.md)
measured, and fixing it is item 3 of what that finding owes.

## How it was found

On 2026-09-25, while checking what the 2026-09-18 finding still owes, Claude
read every caller of `RecordAnswer`, `RecordSilence` and `ResolveAsync`, and
`PrinterWatch` and `PrinterResolver.ResolveAsync` in full. Nothing was observed
on hardware, and nothing measured on FIOS-STB-01 that day depends on it.

## How it happened

Commit `56974b9` (2026-09-20) added the `REQ-RES-008` row, `PrinterReachability`
with its class-level marker, and the header sentence above, together. The type
was written so that a job's outcome *could* be recorded: it has no notion of
where an answer came from. Commit `53f001c`, the same day, added `PrinterWatch`,
which records the watch's own queries. Nothing was ever written to record a
job's. The job path, `LocateConnectionAsync`, dates from `2716421`
(2026-09-17) and was not changed. The marker's claim was not checked against
these two clauses. The type's header names Claude (Claude Opus 5) as its
author.

## What changes

**1. Lookups made together share one query, answered or not.** Built in the
commit that adds this finding, in `PrinterResolver`:

- A lookup that arrives while a query for the same instance is running joins
  it. Every caller waiting on that query receives its outcome: its answer, or
  its failure.
- The query runs with the resolver's own lifetime, not any caller's
  cancellation token. A caller that is cancelled stops waiting, and the query
  goes on for the others. Before this change a cancelled caller ended the
  query, and the next caller asked again.
- A caller that joins takes the running query's timeout, not the one it
  passed. Every caller in the service passes the same configured timeout.
- A lookup for a different instance still waits for the running query to end
  and then asks its own question, because the resolver listens on behalf of
  one query at a time.
- A query's outcome goes only to the callers that were waiting on it. A lookup
  that starts afterwards asks again rather than receiving an old failure.

Five tests in `PrinterResolverTests.cs` cover these, under "Sharing one query".
Two of them failed against the previous code, as they should: three lookups
made together sent three queries, not one; and a cancelled caller made the
other caller ask again, two queries, not one. The other three passed against
the previous code too; they guard the behaviour that did not change. All five
pass against the new code.

Those runs were in Claude's container, on Linux, with Ubuntu's `dotnet-sdk-10.0`
package (10.0.112), which can build the Resolution suite because it needs no
NuGet packages. The Service suite needs one, which the container cannot fetch,
so it was not built there. The dev box's build and test run is the one that
counts.

**2. A job's lookup reaches the schedule.** Not built yet; it is the next
commit. It will also correct `PrinterReachability`'s header. Until then the
`REQ-RES-008` markers still claim a clause the code does not implement.

## Also recorded

The first clause does not say what an unanswered job lookup does to the
schedule. `RecordSilence` cannot simply be reused for it: while the printer is
reachable, each call advances the reconfirmation stream by one step, so a job
that failed early in a record's lifetime would move the next reconfirmation
from 80% of the lifetime to 85%, and the 80% query would never be sent. The
next commit has to settle this, and the README text with it.
