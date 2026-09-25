# The test harness never waited for an async test

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-25. Reviewed by a human before merge.*

**Status: measured in Claude's container, fixed in the commit that adds this
finding. From 2026-09-20 until this commit, 14 async tests were reported as
passing whether they passed or not. Run properly, 13 pass. One had been failing
since the day it was written; the fault was in that test's check, not in the
service.**

## What happened

On 2026-09-25 Claude was testing the change that makes a print job's lookup
reach the reachability schedule, the second clause recorded in
[`2026-09-25-two-clauses-of-req-res-008-were-never-built.md`](2026-09-25-two-clauses-of-req-res-008-were-never-built.md).
As a check that the new tests could fail, the code they test was deliberately
removed. Two new tests in `PrinterWatchTests` still passed. Both are `async`.

## Why

`TestHarness.Run` called each test with `test.Invoke(null, null)` and counted it
as passed if the call returned. An `async` method returns a `Task`, and anything
it throws goes into that `Task` instead of out of the call. That holds even for
a throw before its first `await`. Nothing read the `Task`. So every async test
was counted as passed, however it ended.

The harness's header said:

> A test fails by throwing; anything else is a pass.

That was true only of tests that return nothing.

Three probe tests, run in Claude's container and not delivered, showed it
directly:

| Probe | Reported |
|---|---|
| synchronous, fails | FAIL |
| async, fails before its first `await` | **PASS** |
| async, fails after an `await` | **PASS** |

## What it affected

At `739a2e5` there were 14 async tests, all in the Service suite:

- the 6 in `AvailabilityGateTests`, added in `fb5ba4d` (2026-09-20), each
  tagged `REQ-LIF-006`;
- the 8 in `PrinterWatchTests`, added in `53f001c` (2026-09-20), untagged.

Every run since then counted them as passed. That includes CI, since
`.github/workflows/ci.yml` runs every suite with this harness. The results file
recorded PASS for the six `REQ-LIF-006` tests whatever they did.
`REQ-DIST-009` says a requirement counts as tested only when a test carrying it
is recorded as having *passed*; for these six, the record could not say
otherwise. `REQ-LIF-006` also has a synchronous test that passes,
`MdnsResponderTests.A_withdrawn_responder_answers_nothing`, so SpecCheck's
coverage of it did not rest on these six alone.

No other suite has an async test.

## What the 14 tests really do

Claude ran them with a scratch copy of the harness that waited for the `Task`,
against the code at `739a2e5`. 13 passed. One failed:

```
FAIL  The watch does not blame the printer for the silence
      Expected false: silence does not establish 'off'; nothing here may claim it
```

`PrinterWatchTests.Loss_claims_nothing_it_did_not_measure` checks that the
watch's log never claims anything about the printer. It did so by forbidding
words anywhere in the log, by substring:

```
foreach (string forbidden in new[] { "asleep", "off", "powered", "broken", "adapter" })
```

The watch's own "Printer unreachable" line ends "Asking again on the RFC 6762
schedule, backing off to hourly." "Backing off" contains "off". The service
claims nothing about the printer being off; the check was too crude. The line
and the test both came in `53f001c`, so the test failed from the day it was
written, and nobody saw it.

## The fix

**The harness waits.** `TestHarness.Execute` now runs one test and returns what
it came to, and `Run` uses it:

- A test that returns nothing is judged as before.
- A test that returns a `Task` is waited for. It fails, or is skipped, exactly
  as it would by throwing.
- A test that returns anything else, such as a `ValueTask`, is **failed**. The
  harness cannot wait for it and will not count it as passed.

**The harness is tested.** `TestHarnessTests`, in the Service suite and
registered first, hands `Execute` four probes: an async failure after an
`await`, one before, an async skip, and a `ValueTask`. The tests are synchronous,
so they could fail under the old harness too. Each carries `REQ-DIST-009`. With
the wait removed again on purpose, three of the four failed, as they should.
The fourth checks the new rule for other return types.

**The check is corrected.** `Loss_claims_nothing_it_did_not_measure` now
forbids the phrases that would claim the printer is off: "is off", "turned
off" and "switched off", with "asleep", "powered", "broken" and "adapter" as
before.

Before the fix was written, Claude proposed matching "off" as a whole word.
That would not have worked, because in "backing off" it *is* a whole word. It
was caught while writing the fix.

## Where this was measured

In Claude's container, on Linux, with Ubuntu's `dotnet-sdk-10.0` package. The
Service project needs a NuGet package the container cannot fetch, so the
Service tests there ran in a scratch build. It compiled every Service source
file except the four that need that package (`Program.cs`,
`RefusedStartService.cs`, `ServiceLifecycle.cs`, `WindowsService.cs`), and
every Service test file except `SecurityClaimsTests.cs`, which also relies on
them. The scratch build is not in the repository. The other six suites built
and ran as they are. The dev box's run is the one that counts.

## How it happened

The harness was written for the initial commit (2026-09-05, Claude Opus 4.5),
when every test was synchronous. The first async tests were written on
2026-09-20 (Claude Opus 5), and they passed on their first run. Nothing in the
harness refused a test it could not judge. Nothing in its header said async
tests were not supported. And there was no test of the harness itself.

## Still owed

The job-answer change adds two more async tests to `PrinterWatchTests`. They
were run before this fix, so their passing then meant nothing. They are rerun
under the fixed harness before that change is committed.
*(Added 2026-09-25 by Claude, Claude Opus 5.5: done. The rerun found a defect
in that change itself, which the old harness had hidden. It is recorded in
[`2026-09-25-two-clauses-of-req-res-008-were-never-built.md`](2026-09-25-two-clauses-of-req-res-008-were-never-built.md),
under "The second clause, built".)*
