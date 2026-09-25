# REQ-RES-008 was marked met, but two of its clauses were never built

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-25. Reviewed by a human before merge.*

**Status: found by reading the code, not observed on hardware. Both clauses
are now built: the first in the commit that added this finding (`739a2e5`), the
second in the commit that adds the section "The second clause, built" below.
The `REQ-RES-008` markers now claim what the code does.**

*(Status updated 2026-09-25 by Claude, Claude Opus 5.5. It first read: "One of
the two clauses is built in the commit that adds this finding; the other is not
yet. Until it is, the `REQ-RES-008` markers in the code claim more than the code
does.")*

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
*(Added 2026-09-25: built. See "The second clause, built" below.)*

## Also recorded

The first clause does not say what an unanswered job lookup does to the
schedule. `RecordSilence` cannot simply be reused for it: while the printer is
reachable, each call advances the reconfirmation stream by one step, so a job
that failed early in a record's lifetime would move the next reconfirmation
from 80% of the lifetime to 85%, and the 80% query would never be sent. The
next commit has to settle this, and the README text with it.
*(Added 2026-09-25: settled. Edwin West chose to record answers only. See
below.)*

## The second clause, built

*Added 2026-09-25 by Claude (Anthropic model, Claude Opus 5.5) at the direction
of Edwin West. Reviewed by a human before merge.*

**What a job's lookup does to the schedule.** Edwin West decided: an answer is
recorded, and a silence is not. An answer obtained because a job arrived resets
the schedule like any other answer. A job's lookup that goes unanswered changes
nothing, because the record is still within its lifetime, RFC 6762 §5.2 holds
it until 100%, and the watch's own reconfirmations already decide when it has
gone. The alternative, treating a failed job as a reason to check sooner, was
set aside. RFC 6762 has a section on reacting to a record that turns out to be
unusable. It was not read for this change, and taking it up would be a change
of its own.

**Two answers change nothing.** An answer no newer than the one already held
changes nothing. A job and the watch can share one query and receive the same
answer, and a job's answer can reach the watch after the watch has recorded a
later one of its own. An answer whose lifetime is already over changes nothing
either. Taking it would report the printer reachable only for the next tick to
report it gone.

**The README.** `REQ-RES-008` now says exactly that. Its two clauses read, until
this commit:

> A resolution performed because a job arrived resets the schedule; concurrent
> jobs share one in-flight query rather than issuing one apiece.

They now read:

> An answer obtained by a resolution performed because a job arrived resets the
> schedule like any other answer, unless it is no newer than the answer already
> held or its lifetime is already over; a job's resolution that goes unanswered
> is not counted, because the reconfirmations already decide when the record has
> gone. Concurrent jobs share one in-flight query rather than issuing one apiece,
> whether or not it is answered.

**The code.**

- `PrinterReachability.RecordDemandAnswer` takes a job's answer and applies the
  two rules above. The header sentence quoted earlier in this finding is
  replaced by one that says what happens, and why a silence is not passed to
  `RecordSilence`.
- `PrinterWatch.RecordJobAnswer` queues a job's answer, and the watch's own
  loop records it. The job goes on at once. The reachability state is still
  changed by that one loop only, so it is never touched from two threads, and
  changes are reported one at a time, in order. The loop's wait ends early when
  an answer is queued, because the answer moves the schedule.
- `ServiceHost.LocateConnectionAsync` hands every answer a job's lookup obtains
  to the watch, including one from the resolver's cache, which the first rule
  turns away.

**The tests.** Three in `PrinterReachabilityTests`: a job's answer resets the
schedule, one no newer than the answer held changes nothing, and one whose
lifetime is over does not revive the printer. Two in `PrinterWatchTests`: a
job's answer wakes the watch and moves its next query, and a job's answer while
the printer is held unreachable reports the recovery. One in
`ServiceHostTests`: a job's lookup hands the answer it used to the watch.

The two watch tests are async. They were first written and run before the fix
recorded in
[`2026-09-25-the-test-harness-never-waited-for-an-async-test.md`](2026-09-25-the-test-harness-never-waited-for-an-async-test.md),
when their passing meant nothing, and that is how the fault was found.

Rerun after that fix, in Claude's container, the change failed. As first
written, the watch's loop checked `ChannelReader.Count` to see whether a job's
answer had ended its wait, on a channel created as single-reader. That kind of
channel does not support `Count`; it throws `NotSupportedException`. The loop
would have thrown the first time it came round, and the watch would have
stopped. Ten tests failed with it: seven existing `PrinterWatchTests`, the
`AvailabilityGateTests` case that drives a real watch, and the two new ones.
Under the old harness all ten had reported PASS. The version committed never
reads `Count`. The wait itself now says whether an answer arrived, and the
channel is created with default options.

After that correction, all of them pass. With the recording of a job's answer
removed on purpose, both new watch tests failed. With the two rules in
`RecordDemandAnswer` removed on purpose, the two reachability tests that check
them failed.
