# The start over ran overnight on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-11. Reviewed by a human before merge.*

**Status: measured, once. A build of `5db9063` ran on FIOS-STB-01 from
2026-10-10 21:52:58Z until it was read at 2026-10-11 19:24:22Z, 21 hours 31
minutes, with no restart. It lost the printer 10 times and found it again each
time. In three of those losses the printer-side Wi-Fi had been disconnected,
and each time the service started its questions over 1 to 3 seconds after
Windows reported the Wi-Fi connected again, the printer answered at once, and
it was on offer again within 6 seconds of the reconnect (README `REQ-RES-010`;
[finding](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md)).
Each of those reconnects was made by a script of Edwin West's, not by Windows
and not by SecretPrinter. The longest loss, 2 hours 9 minutes, was not a loss
of the link, and the start over did not act in it. Before that loss 12
connections failed while the printer was still offered. No code changed.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does the printer's host name. Addresses on the
printer-side network are FIOS-STB-01's own (192.168.12.136) and the printer's
(192.168.12.180), as in earlier findings.

**About the times.** Times ending in `Z` are UTC. The service logs UTC.
Windows' event times were printed in UTC. Edwin's script logs US Eastern time,
and its times are given here converted to UTC. The run began on 2026-10-10;
every time from 00:00Z on is 2026-10-11 unless dated.

## What ran

FIOS-STB-01 ran the build of `5db9063` installed on 2026-10-10
([finding](2026-10-10-the-start-over-build-was-installed-on-fios-stb-01.md)):
made on FIOS-STB-01 by `publish-release.ps1`, carrying .NET 10.0.12, not
signed, stamped `0.1.0+5db9063ca3d6e74f94d84073f8966ad361228699`. Read at
19:24:22Z: the service running, the same stamp, and Windows last booted on
2026-10-08 at 00:29:01Z. The log since the install holds one start, at
2026-10-10 21:52:58Z, and no stop.

Edwin's script `Check-SP.ps1` ran every 10 minutes throughout, as it has since
2026-10-09. It is not part of SecretPrinter and is not in this repository. It
reconnects the printer-side Wi-Fi when Windows does not report it up, and
disconnects and reconnects it when the gateway does not answer, and it tests
TCP port 631 on the printer. This changes the host's network configuration,
which the service does not do (`REQ-SEC-007`). That is Edwin's choice for his
machine.

## How it was read

Three read-only commands, run by Edwin in an SSH session on FIOS-STB-01 and
pasted, with link-local and other IPv6 addresses, the printer's host name and
UUIDs replaced before printing:

1. The service's log since 2026-10-10 21:52:58Z: 491 lines. Printed were the
   lines where the service started or stopped, lost or found the printer,
   withdrew or restored the advertisement, reopened or could not reopen its
   printer-side socket, found the adapter usable again or could not examine
   it, and every warning and error. The patterns were checked against the
   texts the code writes, and matched case-sensitively.
2. The script's log since 17:53 Eastern on 2026-10-10: every line where it
   found the Wi-Fi not up or the gateway not answering, and its port-631
   results where they changed.
3. The service's print connections since the install, counted, with every
   completed one listed; and Windows' `WLAN-AutoConfig/Operational` event log
   since 21:52Z on 2026-10-10, each event's time, number and the first line of
   its message.

## The losses

"Not offered for" runs from the line beginning `Printer unreachable` to the
line `Advertisement restored`, as in the finding of 2026-10-10.

| | Lost | Answered again | Not offered for | The printer-side Wi-Fi |
| --- | --- | --- | --- | --- |
| 1 | 10-10 23:20:56Z | 01:30:19Z | 2 h 9 min 26 s | connected throughout |
| 2 | 02:45:18Z | 02:46:53Z | 1 min 38 s | connected throughout |
| 3 | 03:47:18Z | 03:48:52Z | 1 min 37 s | connected throughout |
| 4 | 06:18:33Z | 06:21:17Z | 2 min 47 s | connected throughout |
| 5 | 08:21:37Z | 08:22:34Z | 1 min 0 s | connected throughout |
| 6 | 09:08:25Z | 09:08:39Z | 17 s | connected throughout |
| 7 | 09:54:23Z | 10:00:32Z | 6 min 12 s | disconnected 09:52:45Z to 10:00:29Z |
| 8 | 12:15:38Z | 12:30:30Z | 14 min 55 s | disconnected 12:26:42Z to 12:30:29Z |
| 9 | 13:47:05Z | 13:50:32Z | 3 min 30 s | disconnected 13:46:53Z to 13:50:30Z |
| 10 | 13:52:32Z | 13:53:08Z | 39 s | connected throughout |

"Connected throughout" means Windows logged no disconnect in that loss. The
printer was not lost again between 13:53:11Z and the reading at 19:24:22Z.

In all, the printer was not offered for 2 hours 42 minutes 1 second of the 21
hours 31 minutes 24 seconds, 12.5%. Release 0.1.0's first 22 hours 14 minutes
on FIOS-STB-01, on 2026-10-09 and 2026-10-10, had 14 losses and 30.4%. Those
were different nights. By the arithmetic below, the start over could account
for at most about 7½ minutes of the difference; the rest is not attributed to
anything.

## The three start overs

| | Loss 7 | Loss 8 | Loss 9 |
| --- | --- | --- | --- |
| Windows: "Wireless security failed", then disconnected | 09:52:45Z | 12:26:41Z, 12:26:42Z | 13:46:52Z, 13:46:53Z |
| The service: `Printer unreachable` | 09:54:23Z | 12:15:38Z | 13:47:05Z |
| The service: `Could not reopen the printer-side socket` | 09:54:23Z | none | 13:47:05Z |
| The script: "Wi-Fi status is Disconnected, not Up. Connecting." | 10:00:29Z | 12:30:29Z | 13:50:30Z |
| Windows: connected | 10:00:29Z | 12:30:29Z | 13:50:30Z |
| The service: `The printer-side interface is usable again:` | 10:00:32Z | 12:30:30Z | 13:50:31Z |
| The service: `Printer-side socket reopened on` | 10:00:32Z | none | 13:50:31Z |
| The service: `Printer reachable again` | 10:00:32Z | 12:30:30Z | 13:50:32Z |
| The service: `Advertisement restored` | 10:00:35Z | 12:30:33Z | 13:50:35Z |

Each `usable again` line named `Wi-Fi (192.168.12.136, IPv4 index 27)`, the
address and index the service's other lines that night name. Each `Could not
reopen` line said the interface "holds 192.168.12.136 but is not up".

- **Loss 7.** The Wi-Fi went down 98 seconds before the service held the
  printer unreachable. For those 98 seconds the printer was still offered,
  because its records had not yet expired (`REQ-RES-008`). The script's run
  before had not reconnected anything.
- **Loss 8.** The printer stopped answering while the Wi-Fi was connected. The
  script's run that logged at 12:21:15Z did not reconnect anything, so by its
  own rules it found the Wi-Fi up and the gateway answering, and it found the
  printer's port 631 not answering. The Wi-Fi went down 11 minutes into the
  loss. When the script reconnected it, the printer answered the question
  asked at once. There was no `Could not reopen` line because, by the
  arithmetic below, no question fell while the Wi-Fi was down, and no `socket
  reopened` line because the service writes that only after a logged failure
  or on a new address (`REQ-RES-009`).
- **Loss 9.** The Wi-Fi went down 12 seconds before the loss. The printer was
  found 1 second after the start over. 1 min 57 s after it was offered again
  it was lost again (loss 10), with the Wi-Fi connected, and found at a
  question the schedule put.

**Windows did not reconnect the Wi-Fi by itself in any of the three.** It stayed
down 7 min 44 s, 3 min 47 s and 3 min 37 s, until the script reconnected it.
`docs/operating.md` says the same of earlier drops: Windows' log recorded no
attempt to reconnect, for 13 hours in one case
([finding](2026-09-20-the-wlan-drops-and-nothing-retries.md)). So on
FIOS-STB-01 every start over that night began with Edwin's script.

## Against what a run had to show

The finding of 2026-10-10 listed what a run on a network would need to show.

- **"With the printer held unreachable after the printer-side Wi-Fi was
  disconnected, and the questions already a minute or more apart, a reconnect
  followed within about 5 seconds by the line `The printer-side interface is
  usable again:`, and the printer offered again within seconds after that."**
  Shown three times. Worked out with the arithmetic below, the schedule had
  reached intervals of 256, 512 and 128 seconds.
- **"No such line while the printer is held reachable."** Not exercised on a
  network. No `usable again` line was logged outside a loss, but no reconnect
  happened while the printer was held reachable either: in loss 7 the Wi-Fi
  went down while the printer was offered and came back while it was not.
  The tests show it (`PrinterWatchTests`).
- **"The rest of the service unchanged: a page printed through it."** One page
  printed, at 2026-10-10 21:58:47Z, minutes after the install, as that finding
  records. No page went through the service after that. Whether anyone tried
  to print at 23:19Z is taken up below.

## What it would have been without the start over

Worked out, not run, with the arithmetic of the finding of 2026-10-10: the
first question at the loss, each unanswered question taking the 5-second
resolve timeout, the next due 1 second after it, then 2, 4 and so on, capped
at 3,600 seconds.

- **Losses 1 to 6 and 10** ended within 1 to 3 seconds of a question that
  arithmetic puts (01:30:16Z, 02:46:51Z, 03:48:51Z, 06:21:15Z, 08:22:33Z,
  09:08:38Z and 13:53:07Z). The start over did not act in them, and they are
  found where the schedule alone would have found them.
- **Losses 7, 8 and 9** ended at the start over, 74 seconds after, 181 seconds
  before and 45 seconds after the nearest question of that arithmetic. Without
  a start over the next question would have fallen at 10:03:39Z, 12:33:31Z and
  13:52:00Z: 3 min 7 s, 3 min 1 s and 1 min 28 s later, 7 min 36 s in all.
  That the printer would have answered then is not established.

The arithmetic takes every question to last 5 seconds. Some in losses 7 and 9
were put while the Wi-Fi was down. None ended in an error (no `Printer lookup
failed` line was logged), but how long each took is not logged.

Worked out the same way, no loss was made longer by the start over. The case
the finding of 2026-10-10 warned of, where the printer stays away for an hour or more after the link
returns, did not come up.

## The longest loss was the printer, not the link

Loss 1 ran from 23:20:56Z on 2026-10-10 to 01:30:19Z. Windows logged no
disconnect in it, so there was nothing for the start over to act on. The
script's port-631 results changed at these times:

| Port 631 | Logged at |
| --- | --- |
| answered | 10-10 22:00:46Z, its first run after the install |
| did not | 10-10 23:21:15Z |
| answered | 00:40:45Z |
| did not | 12:21:15Z |
| answered | 12:30:50Z |

So the printer's port answered again by 00:40:45Z, and the script's run
before that had found it closed. The arithmetic puts the service's questions at
23:55:58Z, 00:30:11Z and 01:30:16Z. The service found the printer at 01:30:19Z,
49 min 34 s after the port was first seen answering. That is the hourly cap of
`REQ-RES-008`, which RFC 6762 §5.2 allows and which the start over does not
change. A port that answers is not the printer answering mDNS, so how much
sooner the printer could have been found is not established.

At 01:27:19Z, inside this loss, Windows logged its wireless security stopping
and starting again, with no disconnect. No `usable again` line followed.

## Connections that failed while the printer was still offered

Between 23:19:36Z and 23:20:56Z on 2026-10-10, 12 connections from the client
network failed, all from link-local addresses:

- **9 could not reach the printer.** Each was accepted and failed with "Could
  not connect to the printer at 192.168.12.180:631: No TCP connection to
  192.168.12.180:631 was established within 10s". They ended between
  23:19:46Z and 23:20:38Z; the first opened at about 23:19:36Z by its
  duration.
- **3 could not locate it.** The printer's `_ipps._tcp` instance "did not
  answer within 5s", while the service found nothing wrong locally: the
  adapter was up, held 192.168.12.136, and the address was preferred. They
  ended at 23:20:44Z, 23:20:50Z and 23:20:56Z.

At 23:20:56Z, the second the last one ended, the service held the printer
unreachable and withdrew it. Until then its records had not expired, so it was
offered (`REQ-RES-008`). A job's own question that goes unanswered does not
count towards holding the printer unreachable; the reconfirmations decide that.
The script's run that logged at 23:21:15Z found port 631 not answering.

Whether anyone was printing is not known. Edwin West does not remember. Through
the night and the next day, small groups of connections came and completed,
none carrying more than 7,360 bytes to the printer, against 1,061,107 for the
page of 2026-10-10; groups like that have been seen before. Windows logged its
wireless security stopping and starting again at 23:19:36Z, the second the
first of these connections opened. Whether the two are related is not
established.

This is how `REQ-RES-008` is meant to work, and until this finding it was not
among the README's known problems. A print in the time between the printer going silent and its
records expiring fails. Loss 7 shows the same window from the other side: 98
seconds with the Wi-Fi down and the printer offered.

## Everything else

- **Connections:** 86 accepted; 74 relayed, and all 74 completed; 12 failed, as
  above; none refused. Every relayed connection has an ending line. The only
  page was the one at 21:58:47Z on 2026-10-10, 1,061,107 bytes. The other 73
  completed connections carried between 657 and 88,695 bytes to the printer.
  None came from an IPv4 address.
- **Nothing else warned.** No `Printer lookup failed`, no `Could not examine`,
  and no warning or error but those above. No restart, no reboot.
- **Windows' wireless log** holds three disconnects, those in the table, each
  within a second of "Wireless security failed", and three connects, the
  script's. It also holds 15 times when wireless security stopped, started and
  succeeded within seconds, with no disconnect: 23:19:36Z on 2026-10-10,
  and 01:27:19Z, 02:37:42Z, 02:38:14Z, 05:15:48Z, 06:14:36Z, 09:15:43Z,
  10:15:47Z, 10:40:18Z, 12:13:45Z, 13:00:39Z, 13:18:32Z, 17:22:47Z, 19:07:27Z
  and 19:21:40Z. What Windows means by these is not read here, and no
  relation to the losses is claimed.

## What Claude predicted, and what it did not

Held: the build's stamp, the boot time, the service running, one start and no
stop; the script's three reconnects, "a few seconds before" each start over,
and that each would be for the Wi-Fi not being up or the gateway not
answering; its port-631 result changing to not answering after about 23:19Z
and back before 01:30Z; Windows' disconnects falling before 09:54:23Z, between
12:20:29Z and 12:30:29Z, and before 13:47:05Z, and its connects at the
script's times; no refused connection. The number of losses was said in
advance to be unknown.

Not predicted:

- **The failed connections.** The first reading's prediction named only the
  warnings of a loss. The filter printed them because it keeps every error.
- **Their number.** Claude counted the first reading's lines as 11 and said so
  to Edwin, and predicted 11 for the count. The count printed 12, and the first
  reading holds 12: 9 that could not reach the printer and 3 that could not
  locate it.
- **The 15 times wireless security stopped and started again without a
  disconnect.**
- **The script's first port-631 result after the install** was logged at
  22:00:46Z. Claude had said about 21:5xZ.

## What this does not show

- **That the service says nothing when the adapter comes back while the
  printer is held reachable,** on a network. Shown by tests only.
- **The start over without Edwin's script.** Windows never reconnected the
  Wi-Fi by itself that night. On a machine where nothing reconnects it, the
  start over waits for whatever does.
- **That the printer would have answered at the questions the arithmetic puts**
  in losses 7, 8 and 9, or answered mDNS when its port answered in loss 1.
- **A loss the start over makes longer.** None happened. The finding of
  2026-10-10 works one out from 0.1.0's record.
- **The release build.** This build was made on FIOS-STB-01 and is not signed.
- **Why the Wi-Fi's security failed, why the printer stopped answering, or why
  connections were made at 23:19:36Z.**

## How it was measured

- **The service's log, the script's log and Windows' wireless event log** on
  FIOS-STB-01, by the three commands above, pasted by Edwin West. Filters
  replaced addresses, the printer's host name and UUIDs before printing.
- **The arithmetic,** in a script in Claude's workspace, not in this
  repository: the same model as the finding of 2026-10-10, which was checked
  against 0.1.0's logged times to within 3 seconds. Here it put seven of the
  ten ends within 1 to 3 seconds of a question.
- **Durations** from the logged times, by subtraction.

## What changed

- This finding.
- `README.md`: a note on `REQ-RES-010`; under Known problems, the entry on the
  printer-side network, which said "None of this has been run on a network
  yet.", a new entry on the printer offered after it stops answering, and the
  section's opening note.
- `docs/operating.md`: under "When the printer-side network drops", the
  sentence "Not yet run on a network." replaced, and a note on the change.
- Status notes on the findings of
  [2026-10-10 on the start over](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md)
  and [2026-10-10 on the install](2026-10-10-the-start-over-build-was-installed-on-fios-stb-01.md).
