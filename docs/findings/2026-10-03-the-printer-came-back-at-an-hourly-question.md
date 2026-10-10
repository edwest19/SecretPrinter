# The printer side was lost for two hours and came back at an hourly question

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-03. Reviewed by a human before merge.*

**Status: read from the service's log on hardware (FIOS-STB-01). On 2026-10-03
the service held the printer unreachable from 00:49:02Z to 02:58:27Z. It found
the printer again at the question its schedule puts 129 minutes after a loss,
the second of the hourly ones, and not before. Three shorter losses followed in
the next sixteen minutes, and each ended at a scheduled question too. Edwin
decided that the schedule of `REQ-RES-008`, with its cap of one question an
hour, stays as it is for a release. Why the printer side was lost is not
established. No code changes here.**

*(Status note, 2026-10-10, by Claude, Claude Opus 5.5: release 0.1.0 shipped
with the schedule as it was. On 2026-10-10, after 0.1.0's first 22 hours on
FIOS-STB-01 were read, Edwin West chose the second of the four courses under
"Decided": the questions start over when Windows reports the printer-side
adapter usable again, as README `REQ-RES-010` states. The hourly cap is
unchanged, and so is the rule that only an answer makes the printer reachable.
Worked out from the code, a start over does not make every loss shorter. See
[the finding of 2026-10-10](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md).
Logging each question, raised below, has not been built.)*

All times are UTC and on 2026-10-03 unless a date is given. In US Eastern time,
where the machines are, they fall on the evening of 2026-10-02.

No IPv6 address, hardware address or device name appears in this finding. The
output Edwin pasted holds some; they are household values and stay out of the
repository.

## What was read

- **The last 40 lines of the service log,** pasted by Edwin from FIOS-STB-01.
  The earliest is from 2026-10-02 19:04:57Z and the latest from 03:14:37Z.
  Claude did not see the rest of the log.
- **Three `Test-NetConnection` results and one `Get-NetAdapter` result,** pasted
  with them. They carry no times.
- **The build** is `64e8fc2` by the session record, started 2026-10-02
  16:04:24Z. None of the 40 lines is a start line.

## What the log says

| Time | Line, shortened |
| --- | --- |
| 2026-10-02 19:04:57 | Four connections completed. The paste shows nothing after them until the next row. |
| 00:49:02 | `Printer unreachable`; `No longer accepting print jobs on Ethernet`; `Advertisement withdrawn and the listener closed` |
| 02:58:27 | `Printer reachable again` |
| 02:58:28 | `Probe clear`; `Accepting print jobs` on 192.168.1.161:631 and on the client interface's link-local address |
| 02:58:30 | `Advertisement restored` |
| 03:02:06 | `Printer unreachable`, withdrawn |
| 03:03:05 | `Printer reachable again`; restored at 03:03:08 |
| 03:09:56 | `Printer unreachable`, withdrawn |
| 03:10:33 | `Printer reachable again`; restored at 03:10:36 |
| 03:14:10 | `Printer unreachable`, withdrawn |
| 03:14:34 | `Printer reachable again`; restored at 03:14:37 |

Each recovery was followed by `Probe clear` and by both listeners opening
again. At 03:03:05, 03:10:33 and 03:14:34 a line `Probing for the advertised
names before offering them` comes first. At 02:58 the paste shows no such line
(see "Not established").

## The schedule, worked out from the code

The log does not record the questions the service puts to a printer it holds
unreachable. It writes one line when the printer is lost and one when it
answers. The times of the questions between can be worked out:

- `PrinterReachability.FallUnreachable` makes the first question due at once.
- Each unanswered question makes the next wait one second, then double the last
  wait, up to 60 minutes (`RecordSilence`, `MaximumRetryInterval`).
- An unanswered question takes the resolve timeout to fail. The default is 5
  seconds (`TuningSettings.Default`). The value in force on FIOS-STB-01 was not
  read; the startup log prints it.

With a 5-second timeout and no answers, the questions fall at these times after
the loss:

| Question | Asked after | Then waits |
| --- | --- | --- |
| 1 | 0 s | 1 s |
| 2 | 6 s | 2 s |
| 3 | 13 s | 4 s |
| 4 | 22 s | 8 s |
| 5 | 35 s | 16 s |
| 6 | 56 s | 32 s |
| 7 | 1 min 33 s | 64 s |
| 8 | 2 min 42 s | 128 s |
| 9 | 4 min 55 s | 256 s |
| 10 | 9 min 16 s | 512 s |
| 11 | 17 min 53 s | 1,024 s |
| 12 | 35 min 2 s | 2,048 s |
| 13 | 69 min 15 s | 3,600 s |
| 14 | 129 min 20 s | 3,600 s |

Claude worked this table out earlier in the same session, to read the outage of
2026-10-02, before Edwin pasted this log. It was not adjusted afterwards. It
leaves out the time taken to reopen the printer-side socket before each
question (`REQ-RES-009`) and the time an answer takes to arrive.

## The log against the schedule

| Lost | Answered | Time between | Nearest question in the table |
| --- | --- | --- | --- |
| 00:49:02 | 02:58:27 | 129 min 25 s | 14, at 129 min 20 s |
| 03:02:06 | 03:03:05 | 59 s | 6, at 56 s |
| 03:09:56 | 03:10:33 | 37 s | 5, at 35 s |
| 03:14:10 | 03:14:34 | 24 s | 4, at 22 s |

Each `reachable` line comes 2 to 5 seconds after a question in the table. So:

- **The service found the printer at a scheduled question every time,** as
  `REQ-RES-008` specifies.
- **In the long loss the question before the one that was answered was number
  13,** at about 01:58:17. It was not answered. So the printer began answering
  on the printer side at some time between about 01:58 and 02:58, and the
  service did not learn of it until 02:58:27. When in that hour is not known.
- **This is the wait [`operating.md`](../operating.md) describes** under "When
  the printer-side network drops": after a long outage the printer may not
  reappear for up to an hour after the link returns.
- **The timing fits the default 5-second timeout.**

## The printer answered between the short losses

The time from each `reachable` line to the next `unreachable` line was 219, 411
and 217 seconds. `REQ-RES-008` reconfirms a record at 80 to 82% of its lifetime
and holds the printer unreachable when the record reaches 100% unanswered. With
a lifetime of 120 seconds, one answered reconfirmation followed by silence
gives 216 to 218 seconds, and three give 408 to 415. The three times fit one,
three and one answered reconfirmations to within about a second.

The lifetime of the printer's records was not read from this log, so this is a
fit and not a reading. If it is right, the printer last answered about two
minutes before each `unreachable` line, and answered normally between the
losses.

## The connection tests

In the order Edwin ran them, with no times:

1. `Test-NetConnection 192.168.12.180 -Port 631` failed. It reported
   `InterfaceAlias : Ethernet` and `SourceAddress : 192.168.1.161`, and the
   ping timed out.
2. The same again, with the same result.
3. `Get-NetAdapter` showed `Ethernet` up at 1 Gbps and `Wi-Fi`, the
   printer-side adapter, up at 260 Mbps.
4. `Test-NetConnection` again succeeded, over `Wi-Fi`, from 192.168.12.163.

Edwin did not reconnect the Wi-Fi by hand.

In tests 1 and 2 Windows chose its default route, on the client side, for an
address on the printer's network. This is the third occasion on which that has
been measured. The first two are in the findings of
[2026-09-22](2026-09-22-connections-to-the-printer-leave-by-the-default-route.md)
and
[2026-09-27](2026-09-27-the-printer-went-silent-and-the-join-held.md), and the
README's fifth point under "Read this before installing" describes them. That
point is updated with this finding to say three.

What state the printer-side adapter was in when tests 1 and 2 ran was not read.

## A wrong reading, withdrawn

Claude first told Edwin that tests 1 and 2 showed Windows had no route to the
printer's network through `Wi-Fi` at that moment. They do not show that. The
2026-09-27 finding records Windows choosing `Ethernet` for the printer's address
while `Wi-Fi` was up, with its address and its route. Claude had not reread
that finding, or the README's fifth point, before saying it. The tests show
which adapter Windows chose and nothing about why. Claude corrected this to
Edwin before writing this finding.

## Decided

Edwin, in this session: the schedule stays as it is for a release, hourly cap
included.

Claude had put four courses to him: keep the schedule and log each question;
also start the schedule again when Windows reports a change on the printer-side
adapter; also take the printer's own multicast records, heard while it is held
unreachable, as an answer; or cap the wait below an hour. The last would depart
from RFC 6762 §5.2, which says the intervals between repeated questions MUST
increase by at least a factor of two and that a querier MAY cap them once they
reach 60 minutes. None of the four was built.

## What this says about the outage of 2026-10-02

Item 8 under "Also seen" in
[the 2026-10-02 finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md)
says that, about an hour after a restart at 13:52 went unanswered, a connection
to the printer worked and the probe tool got its replies "with the service
still unanswered". By the table above, a service started at 13:52 and never
answered asks at about 35 minutes and about 69 minutes after its start, which
is about 14:27 and about 15:01, and not between. So the service was not asking
when the probe tool was run, and "still unanswered" says nothing about whether
it could have heard the printer then. The start is known only to the minute.
That finding gains a status note saying so.

The probe tool also asks for unicast replies, as the
[2026-09-19 finding](2026-09-19-the-printer-side-multicast-membership-is-lost.md)
records, and the service needs the printer's multicast answer. A reply to the
probe tool shows the printer is up. It does not show the service can hear it.

## Not established

- **Why the printer side was lost** at 00:49, or at 03:02, 03:09 and 03:14.
- **When the printer began answering again** in the hour before 02:58.
- **The state of the printer-side adapter during the losses.** No adapter
  state, WLAN event or multicast membership was read while the printer was
  held unreachable.
- **When the connection tests were run,** and so which loss, if any, tests 1
  and 2 fell in.
- **What brought the Wi-Fi back.** Nobody reconnected it by hand. On two other
  adapters nothing reconnected them
  ([2026-09-20](2026-09-20-the-wlan-drops-and-nothing-retries.md)); this
  adapter is a different one.
- **Whether the log lacks a `Probing for the advertised names` line at 02:58.**
  The paste shows `Probe clear` at 02:58:28 with no such line before it. Lines
  have gone missing from console pastes before. The log file was not searched.
- **The resolve timeout in force on FIOS-STB-01.**

## Raised, not decided

- **Logging each question put to an unreachable printer,** with the time the
  next is due. With it, the times in the table above would be read and not
  worked out. Claude proposed it with the first of the four courses. Edwin has
  not said yes or no to it.
- **A written procedure for what to read during an outage, before touching
  anything.** The 2026-10-02 finding could not say what restored the printer:
  the restart that was answered came 16 seconds after the listen tool joined
  the multicast group, and what the service would have heard before either
  was not read.
