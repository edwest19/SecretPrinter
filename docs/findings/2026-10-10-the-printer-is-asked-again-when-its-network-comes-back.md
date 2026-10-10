# The printer is asked again when its network comes back

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-10. Reviewed by a human before merge.*

**Status: built and measured against the code by tests; not run on a network.
The service now examines the printer-side adapter every 5 seconds. When the
adapter is usable again while the printer is held unreachable, it asks the
printer at once and starts the RFC 6762 §5.2 questions over from one second
(README `REQ-RES-010`). Edwin West decided this on 2026-10-10, after the first
22 hours of release 0.1.0 on FIOS-STB-01 were read. It does not make every loss
shorter. Worked out from the code against 0.1.0's record, and not run: in one
loss it could have brought the printer back about 37 minutes sooner, if the
printer was answering mDNS then; in another it would have brought it back 26 to
35 minutes later than 0.1.0 did.**

No IPv6 address, hardware address, network name or device name appears in this
finding. Neither does an address in `169.254.0.0/16` that the service's log
records for the printer-side adapter. The addresses in the tests are made up.

**About the times.** Times ending in `Z` are UTC. Those from 0.1.0 come from the
service's log on FIOS-STB-01. Those from Edwin's script were logged in US
Eastern time and are given here in UTC. The losses ran from 2026-10-09 into
2026-10-10, and each is dated where it starts.

## Why this was done

### What 0.1.0 did on FIOS-STB-01

Release 0.1.0 started on FIOS-STB-01 at 2026-10-09 17:39:30Z. On 2026-10-10
Edwin pasted its log up to 15:53:42Z, read with a filter for the lines where it
lost or found the printer, reopened its printer-side socket, started, stopped
or warned. In those 22 hours 14 minutes the service was not restarted, and it
lost the printer 14 times and found it again each time by itself.

| | Lost | Answered again | Not offered for | The service's log says the adapter was not usable |
| --- | --- | --- | --- | --- |
| 1 | 10-09 18:53:08Z | 19:11:03Z | 17 min 58 s | |
| 2 | 10-09 19:34:10Z | 19:34:34Z | 27 s | |
| 3 | 10-09 19:57:43Z | 20:14:47Z | 17 min 7 s | yes, at 19:57:43Z |
| 4 | 10-09 20:44:23Z | 23:53:46Z | 3 h 9 min 26 s | yes, at 21:02:18Z |
| 5 | 10-10 01:13:31Z | 01:13:44Z | 16 s | |
| 6 | 10-10 01:17:24Z | 01:17:24Z | 3 s | |
| 7 | 10-10 03:05:13Z | 03:23:09Z | 17 min 59 s | |
| 8 | 10-10 03:39:06Z | 03:41:14Z | 2 min 11 s | yes, at 03:39:06Z |
| 9 | 10-10 05:09:07Z | 07:18:30Z | 2 h 9 min 26 s | |
| 10 | 10-10 09:38:24Z | 09:38:38Z | 17 s | |
| 11 | 10-10 10:15:08Z | 10:20:05Z | 5 min 0 s | |
| 12 | 10-10 11:34:20Z | 11:52:15Z | 17 min 57 s | yes, at 11:34:20Z |
| 13 | 10-10 14:38:14Z | 14:40:58Z | 2 min 47 s | |
| 14 | 10-10 14:59:09Z | 15:04:06Z | 5 min 0 s | |

"Not offered for" runs from the line beginning `Printer unreachable` to the line
`Advertisement restored`, a few seconds after `Printer reachable again`. In all,
the printer was not offered for 6 hours 45 minutes 54 seconds, 30.4% of the
time.

The last column is the line beginning `Could not reopen the printer-side
socket`. The service writes it when it is about to ask a printer it holds
unreachable and finds the adapter not usable, once for each reason
(`REQ-RES-009`). It says nothing when no question falls while the adapter is
down, so an empty cell does not show that the adapter stayed up.

### Edwin's script

FIOS-STB-01 also runs a script of Edwin's, `Check-SP.ps1`. Its first run was at
2026-10-09 20:27:35Z, and it runs every 10 minutes as a scheduled task. It is
not part of SecretPrinter and is not in this repository. By its own header, Claude wrote it in another
conversation, on 2026-10-09, at his request. It reconnects the printer-side
Wi-Fi when Windows does not report it up, disconnects and reconnects it when
the gateway does not answer a ping, and tests TCP port 631 on the printer. The
first two change the host's network configuration, which the service does not
do (`REQ-SEC-007`). That is Edwin's choice for his machine.

Its log, read the same day, says it reconnected the Wi-Fi at 21:10:28Z on
2026-10-09, and at 03:40:29Z, 06:40:29Z and 11:40:29Z on 2026-10-10: inside
losses 4, 8, 9 and 12. It records one more reconnect, at 20:31:57Z on
2026-10-09, between losses 3 and 4 and off its 10-minute schedule, and none
inside the other losses. Its port-631 results changed at these times:

| Port 631 | At |
| --- | --- |
| answered | 10-09 20:27:57Z, its first run |
| did not | 10-09 20:51:15Z |
| answered | 10-09 23:40:44Z |
| did not | 10-10 05:11:15Z |
| answered | 10-10 06:40:50Z |
| did not | 10-10 15:01:22Z |
| answered | 10-10 15:10:45Z |
| did not | 10-10 17:21:05Z |
| answered | 10-10 17:30:44Z |

So, by that measure:

- **Loss 4:** the port did not answer at any run from 20:51:15Z to the one
  at about 23:30Z, more than two hours after the Wi-Fi was reconnected at
  21:10:28Z. It was first seen answering at 23:40:44Z. 0.1.0 offered the
  printer again at 23:53:49Z, 13 minutes 5 seconds later.
- **Loss 9:** the port did not answer from 05:11:15Z, while the Wi-Fi was up
  and the gateway answered. At 06:40:29Z the script found the Wi-Fi not up and
  reconnected it, and the port answered at 06:40:50Z. 0.1.0 offered the printer
  again at 07:18:33Z, 37 minutes 43 seconds later.
- **Loss 12:** the script reconnected the Wi-Fi at 11:40:29Z, and the port
  answered in that run. 0.1.0 offered the printer again at 11:52:17Z, about 12
  minutes later.
- **Loss 7:** the port answered at the runs of about 03:10Z and 03:20Z while
  0.1.0 held the printer unreachable and its questions went unanswered. Why is
  not established.

The last two come after the end of the service's log that was read. The run
of 17:20Z fell while FIOS-STB-01's service was stopped for the run of
[another finding](2026-10-10-questions-carrying-their-answers-went-unanswered-on-a-network.md);
why the port did not answer then is not known.

A port that answers is not the printer answering mDNS. Loss 7 shows the two can
differ.

### What the delays were

0.1.0 asks a printer it holds unreachable after 1, 2, 4, 8… seconds, up to once
an hour (`REQ-RES-008`), and does not log those questions. The 2026-10-03
[finding](2026-10-03-the-printer-came-back-at-an-hourly-question.md) worked out
when they fall. The same arithmetic is used below. It starts the first question
at the loss and has each unanswered question take the 5-second resolve timeout,
after which the next is due 1 second later, then 2, 4, and so on, capped at
3,600 seconds. Against the times 0.1.0 logged, it gives:

| Loss | Logged | Worked out |
| --- | --- | --- |
| 4 | 21:02:18Z, 21:19:22Z, 23:53:46Z | 21:02:16Z, 21:19:25Z, 23:53:48Z |
| 9 | 07:18:30Z | 07:18:27Z |
| 12 | 11:43:37Z, 11:52:15Z | 11:43:36Z, 11:52:13Z |

Each is within 3 seconds. So in losses 4, 9 and 12 0.1.0 found the printer at
a question its schedule put, and not sooner. In loss 12 the question before,
at 11:43:37Z, went unanswered, about three minutes after the script had found
the port answering.

## Decided

At a pause on 2026-10-10 Claude put three courses to Edwin for the hourly
schedule, and recommended starting the questions over when Windows reports the
adapter usable again. Edwin, at 13:52 Eastern: "lets do Start the schedule over
when Windows reports the printer-side adapter usable again". It is the second of
the four courses put to him on 2026-10-03, when he decided that the schedule
would stay as it was for a release. Release 0.1.0 shipped with the schedule as
it was.

While building it, Claude worked out what it would have done in 0.1.0's
losses. In one it would have brought the printer back later, which Claude had
not known when recommending it. Claude told Edwin before delivering. That is
the next section.

## What it would have done in 0.1.0's losses

Worked out from the code with the arithmetic above, and not run. A start over
needs the adapter seen not usable and then usable. The service's log shows the
adapter not usable in losses 3, 4, 8 and 12, and the script found the Wi-Fi not
up in loss 9. The watch would see a drop of 5 seconds or more. How long each
lasted is not known. In losses 4, 8, 9 and 12 the script reconnected the
Wi-Fi.

| Loss | 0.1.0 found the printer | With a start over |
| --- | --- | --- |
| 3 | 20:14:47Z | The adapter came back at a time not known, between 19:57:43Z and 20:14:47Z, without the script, which was not yet running. The printer would have been asked within about 5 seconds of that. Up to about 17 minutes sooner, if it answered then. |
| 4 | 23:53:46Z | Started over between 21:10:28Z and 21:19:22Z. By 22:20Z to 22:29Z the questions are an hour apart again, falling at 23:19Z to 23:28Z, while the port was still closed, and next at 00:19:53Z to 00:28:47Z. Assuming the printer did not answer mDNS while its port was closed, it is found **26 to 35 minutes later** than 0.1.0 found it. |
| 8 | 03:41:14Z | Started over at about 03:40:29Z. Under a minute sooner, if the printer answered then. |
| 9 | 07:18:30Z | Started over at about 06:40:29Z, with questions at 06:40:29Z, :35, :42, :51, 06:41:04Z… and the port answering at 06:40:50Z. **About 37 minutes sooner,** if the printer answered mDNS when its port did. |
| 12 | 11:52:15Z | Started over between 11:40:29Z and about 11:40:50Z, with questions at about 11:43:11Z to 11:43:32Z, 11:45:24Z to 11:45:45Z, 11:49:45Z to 11:50:06Z and 11:58:22Z to 11:58:43Z. The printer did not answer 0.1.0 at 11:43:37Z and did at 11:52:15Z. It is found about 6½ minutes sooner if it began answering by about 11:45:24Z, about 2 minutes sooner if by about 11:49:45Z, and **about 6 minutes later** if after about 11:50:06Z. Which is not known. |

In the other nine losses nothing shows the adapter down, and the start over
changes nothing in them unless the adapter went down unseen. In loss 14 the
script's run of 15:00:35Z found the gateway answering and the printer's port
not.

The pattern, worked out the same way: a start over never puts the questions
further apart than they would have been, but it moves the moment each one
falls. It helps when the
printer answers soon after its network is back. When the printer itself stays
away for an hour or more after that, the questions are an hour apart either
way, and which schedule finds it first is chance, as in loss 4. RFC 6762 §5.2
lets the intervals stop doubling only once they reach 60 minutes, so nothing
within it removes that wait.

## What RFC 6762 says

RFC 6762 does not require this.

- §5.2, continuous querying: the first two questions "MUST be at least one
  second" apart, each interval "MUST increase by at least a factor of two", and
  once an interval reaches or exceeds 60 minutes "a querier MAY cap the interval
  to a maximum of 60 minutes".
- §5.4 speaks of "A Multicast DNS querier sending its initial batch of
  questions immediately on wake from sleep or interface activation". The
  sentence is about the unicast-response bit in those questions. It takes such
  a batch as given.
- §10.3, "Cache Flush on Topology change": "If the hardware on a given host is
  able to indicate physical changes of connectivity, then when the hardware
  indicates such a change, the host should take this information into account
  in its Multicast DNS cache management strategy." Its examples are about
  records held in a cache when a cable is disconnected.

The service starts a new run of §5.2 questions when Windows reports the adapter
usable again. Within each run the one-second minimum and the doubling hold.

## What the service does now

- **`AdapterWatch`, new.** From when the printer watch starts, after the
  startup probe and announcement, until the service stops, it examines the
  printer-side adapter every 5 seconds by `StartupWait.Examine`, the rule the
  startup wait uses (`REQ-LIF-008`): up, holding exactly one IPv4 address,
  that address outside `169.254.0.0/16` and not reported tentative, deprecated
  or invalid. It reports the interface when it is usable after being seen not
  usable, and when it is usable with a different address or index than when
  last seen. It starts from the interface the service started with. It reports
  nothing else, so an adapter that stays usable starts nothing over.
- **A failed examination** is logged as a warning when it begins, when it
  changes, and when it comes back after an examination that worked, and it is
  taken for no change either way. `StartupWait.Examine` turns the failures it
  knows of into a reason; the handling is for any other, so that the service
  goes on.
- **`PrinterWatch.StartQuestionsOver`** takes the report and wakes the watch's
  loop. On the loop, if the printer is held unreachable, the questions start
  over (`PrinterReachability.RestartContinuousQuerying`): the next is due at
  once, and the interval after it is one second, doubling from there. The
  service logs one line, as information: `The printer-side interface is usable
  again: <interface>. Asking the printer now, and starting the RFC 6762
  questions again from one second.` While the printer is held reachable the
  report changes nothing and nothing is logged.
- **The question asked at once** is asked as every question to a printer held
  unreachable is: the printer-side socket is reopened on the adapter's current
  address first (`REQ-RES-009`), and the question goes out on the printer-side
  interface only (`REQ-RES-006`).
- **Only an answer makes the printer reachable.** The start over takes no
  argument, so the adapter's state still cannot reach `PrinterReachability`.
  The note on that type, which said no schedule there could rest on adapter
  state, now says no verdict can, and that the questions can be started over.
- **`ServiceHost.RunAsync`** creates the adapter watch next to the printer
  watch and runs it with the service's other parts.

Not changed: the startup wait, which keeps its own sequence; the
reconfirmations while the printer is held reachable; the hourly cap.

## Security

The adapter watch opens no socket and sends nothing. It reads the adapter list
Windows gives, as the startup wait does. What it can cause is a question to the
printer, on the printer-side interface only, sooner than the schedule would have
asked it. Nothing on the client side changes.

If the adapter went down and up again every 10 seconds, or changed address at
every examination, the printer would be asked again that often for as long as
it went on. A question put at a start over comes after the previous question
has ended: when that question was sent and went unanswered, at least the
resolve timeout after it. Not seen, and not tested.

## The tests

Eleven new tests. Ten were written first and run against stubs that compiled
and did nothing: seven failed and three passed. The eleventh was added later
and is explained below the table. After the change the stubs were put back once
more, with all eleven, and the table gives that run; the ten agree with the
first:

| Test | Against the stubs |
| --- | --- |
| Starting over while the printer is held unreachable makes the next question due at once, and the interval after it one second again | failed |
| Starting over while the printer is held reachable changes nothing | passed |
| After starting over, the printer is reachable again only when it answers | passed |
| Told the interface is usable again while the printer is held unreachable, the watch asks at once, starts from one second again, and says so once | failed |
| Told the interface is usable again while the printer is held reachable, the watch changes nothing and says nothing | failed |
| An interface seen not usable and then usable again is reported once | failed |
| An interface that stays usable is never reported | passed |
| A usable interface found on a new address is reported | failed |
| An examination that fails is logged once, is not taken for a change, and does not end the watch | failed |
| The watch examines the adapter every five seconds until it is stopped | failed |
| A failed examination that ends and comes back is logged again | failed |

The test with the printer held reachable fails against the stubs because it
waits for the watch to wake, and a stub never wakes it. The three that pass
guard against doing too much. The eleventh was added when the warning's text
was made to say that a failure that ends and comes back is logged again; until
then the text said a failure is logged again only if it changes, which the code
did not do.

After the change, in Claude's workspace: the resolution suite 44 tests and the
service suite 113, all passing. The other five suites are unchanged.

## Breaking each claim on purpose

Seventeen mutations, each made alone, built, run against every suite that
includes the changed file, and undone:

| Mutation | Caught by |
| --- | --- |
| Starting over while held reachable | Starting over while reachable; the watch held reachable |
| The interval not reset | Starting over while unreachable; the watch held unreachable |
| The next question not made due at once | the same two |
| Starting over making the printer reachable | Starting over while unreachable; only an answer (the service suite does not catch it) |
| The report not taken in on the loop | both watch tests |
| The wait not woken by the report | both watch tests |
| The line logged as a warning | the watch held unreachable |
| A line logged while held reachable | the watch held reachable |
| No line logged | the watch held unreachable |
| Every usable examination reported | five of the six adapter watch tests |
| The last interface seen never updated | three adapter watch tests |
| A failure never cleared | the failure that comes back |
| A failure taken for the adapter not usable | both failure tests |
| A failure logged every time | the failure logged once |
| Ten seconds in place of five | the five-second test |
| The watch never examining | the five-second test |
| **The adapter watch not started in `ServiceHost.RunAsync`** | **no test** |

No test runs `ServiceHost.RunAsync`, so nothing but a run would show that the
service starts the adapter watch.

## What a run on a network would need to show

- With the printer held unreachable after the printer-side Wi-Fi was
  disconnected, and the questions already a minute or more apart, a reconnect
  followed within about 5 seconds by the line `The printer-side interface is
  usable again:`, and the printer offered again within seconds after that.
- No such line while the printer is held reachable.
- The rest of the service unchanged: a page printed through it.

## What this does not show

- **Any of it on a network, on Windows, or in the release build.** The wiring in
  `ServiceHost.RunAsync` is not run by any test.
- **That Windows reports the adapter not usable for 5 seconds or more in the
  drops that matter.** The service's log shows the adapter not usable in four
  of 0.1.0's losses, at the moments it asked.
- **That the printer answers mDNS as soon as its port does.**
- **What would have happened in 0.1.0's losses.** The figures above are worked
  out from the code and the two logs, not measured.
- **Any change to the losses where the printer, not the link, was away.**

## How it was measured

- **0.1.0's log** on FIOS-STB-01, pasted by Edwin, read with filters that
  replace addresses, the printer's host name and UUIDs. The loss times and the
  adapter's state come from it.
- **The script's log,** pasted by Edwin: its reconnects, and its port-631
  results, reduced by a command to the runs where the result changed.
- **The tests,** in Claude's workspace, on Linux, with .NET SDK 10.0.112 and
  runtime 10.0.12. The six test projects that build directly were built and
  run. The service and its test project were built from the repository's own
  files by two scratch projects outside the repository, which take
  `System.ServiceProcess.ServiceController` from the SDK's own folder because
  NuGet cannot be reached; that is not the build a release makes. Suites: 28,
  27, 34 (9 skipped, for lack of IPv6), 61, 44, 136, and 113 in the service
  suite.
- **SpecCheck,** with the scratch service build added: 101 requirements, 23
  assemblies, 18 evidence rows, 458 test records, 83 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains one row, `REQ-RES-010`, `OK`, and no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.
- **The arithmetic,** in a script in Claude's workspace, not in this
  repository, checked against the times 0.1.0 logged.

## What changed

- `src/SecretPrinter.Service/AdapterWatch.cs`, new.
- `src/SecretPrinter.Service/PrinterWatch.cs`: `StartQuestionsOver`, and the
  loop and its wait taking the report in.
- `src/SecretPrinter.Resolution/PrinterReachability.cs`:
  `RestartContinuousQuerying`, and the `REQ-RES-008` note on the type.
- `src/SecretPrinter.Service/ServiceHost.cs`: the adapter watch started.
- Tests: three in `PrinterReachabilityTests.cs`, two in `PrinterWatchTests.cs`,
  six in the new `AdapterWatchTests.cs`.
- `README.md`: a new row, `REQ-RES-010`; a dated note on `REQ-RES-008`; the
  Known problems entry on the printer-side network rewritten.
- `docs/operating.md`: the first bullet under "When the printer-side network
  drops" rewritten.
- Status notes on the findings of
  [2026-10-03](2026-10-03-the-printer-came-back-at-an-hourly-question.md) and
  [2026-10-09](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md).
