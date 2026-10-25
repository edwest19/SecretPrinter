# The service heard the printer come back, on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-22. Reviewed by a human before merge.*

**Status: measured, once. A build of `5a291c4`, made from source on
FIOS-STB-01, was installed there in place of release 0.4.0 at 21:33Z. Edwin
West switched the printer off for about 16 minutes. The service held it
unreachable from 21:53:39Z, and at 22:08:40Z, no more than about 40 seconds
after it was switched on, logged `Printer reachable again: it was heard on the
printer-side interface without being asked` and had it on offer again 3
seconds later (README `REQ-RES-011`;
[finding](2026-10-22-the-service-hears-the-printer-announce-itself.md)).
Worked out from the code, its next question was not due until about 22:11:32Z.
A page printed through it before the test and another after. Not expected: at
22:13:54Z the service lost the printer again, its questions unanswered, and
found it at 22:18:51Z by one of its own questions, at the time the code's
schedule gives; why the printer went unanswered is not shown. FIOS-STB-01 now
runs this build, which is not signed and carries .NET 10.0.12, one security
release behind the 10.0.13 that 0.4.0 carries. No code changed.**

*(Status note, 2026-10-25, by Claude, Claude Opus 5.5: this build's first
three days on FIOS-STB-01 were read there on 2026-10-25, read-only, from the
service's log, from the build's first line at 21:33:42Z on 2026-10-22 to the
reading at 20:54:32Z on 2026-10-25: 71 hours 20 minutes 50 seconds. Nothing was
run, restarted or changed for it. Windows last started at 20:55:31Z on
2026-10-14, the service was running, and the log holds one start in the
period, this build's, stamped `0.4.0+5a291c45bac4b2e72fdc35cfaa36bfce8e62f3b8`.
It accepted 84 connections, each located from the resolver's cache, and
refused none. 72 were relayed with the printer's certificate matching the
pinned fingerprint and completed, and 12 failed, all with `Could not connect to
the printer`. Eleven of those are the test's, in the minute the printer was
switched off. The twelfth was logged in the minute from 15:14Z on 2026-10-23,
1 to 2 minutes after the printer had been offered again at 15:13:00Z; why it
failed is not shown. Every client address was an IPv6 link-local address,
three in all, and none was IPv4. Which connections carried pages is not in the
log; none after the test carried a million bytes or more, as each page of the
test did. **The service lost the printer 33 times, the test's two among them,
and found it again each time without a restart. It heard the printer without
being asked once, in the test, and in none of the 31 losses after it.** Of
those 31, 28 ended with `it answered` 0 to 4 seconds after a question was due
by the schedule worked out from the code under "The arithmetic" below, carried
on past 1,073 seconds: 2,102 and 4,155 seconds, then every 3,605, an hour's
wait and a question's 5 seconds. The other three ended with `it answered`
after a `The printer-side interface is usable again` line. The printer was not offered for
6 hours 29 minutes 50 seconds in all, 9.1% of the period. One loss, from
09:46:43Z to 13:56:20Z on 2026-10-25, accounts for 4 hours 9 minutes 37
seconds of that; it ended with `it answered` at 13:56:17Z, 4 seconds after the
question due 14,970 seconds after the loss, and the one due an hour before that
went unanswered, or the log would say so. When the printer could have answered
again, and whether it announced itself in any of the losses, is not shown. The
other 32 losses lasted 2 hours 20 minutes 13 seconds together, the longest 35
minutes 8 seconds. No warning or error was logged but those of the losses and
the failures: no `Printer lookup failed` line and no `Could not reopen the
printer-side socket` line. Release 0.3.0, read the same way over 74 hours 10
minutes from 2026-10-15, lost the printer 34 times and did not offer it for
14.8% of the time, with one loss of 10 hours 14 minutes while the printer did
not answer on its own network
([finding](2026-10-18-the-third-release-ran-three-days-on-fios-stb-01.md));
those were other days, and nothing here attributes the difference to either
build. Edwin's words on 2026-10-25: "it has been working great". The read was
one command, run by Edwin in his SSH session and attached: it grouped every
line that is not a `Job:` line, with addresses, the printer's host name, UUIDs
and numbers masked, counted the `Job:` lines, and listed each withdrawal with
how it ended. The arithmetic was done in Claude's workspace.)*

*(Status note, later on 2026-10-25, by Claude, Claude Opus 5.5: release 0.5.0,
published that day and carrying this change, replaced this build on FIOS-STB-01
at 22:29Z by the update steps with the signature check, and an iPhone printed a
page through it. FIOS-STB-01 no longer runs this build. See [the finding of the
release](2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does the printer's host name or any client's address.
Addresses on the printer-side network are FIOS-STB-01's own (192.168.12.136)
and the printer's (192.168.12.180), as in earlier findings. Every client
address in the lines read was an IPv6 link-local address, replaced before
printing.

**About the times.** Times ending in `Z` are UTC, from the service's log on
FIOS-STB-01, to the second. Edwin's times are by his iPhone's clock, to the
minute, in US Eastern time (UTC-4), and are given here converted. **The two
clocks were not compared.** Everything here happened on 2026-10-22.

## Why this was run

[The finding of earlier today](2026-10-22-the-service-hears-the-printer-announce-itself.md)
built `REQ-RES-011` and listed what a run on a network would need to show:

1. "With the printer held unreachable and the questions already minutes apart,
   the printer switched off and on: the line `Printer reachable again: it was
   heard on the printer-side interface without being asked`, within seconds of
   the printer's first announcement, and the advertisement restored after it."
2. "While the printer is held reachable, no such line when it announces
   itself."
3. "A page printed through it afterwards."

Edwin chose "Plan the run on FIOS-STB-01 (Recommended)". Claude then put the
question of what to run there, with three options: "Release 0.5.0
(Recommended)", "Build from source there" and "Not today". Edwin chose "Build
from source there", and accepted that the build would carry an older .NET
runtime than release 0.4.0 does.

## What ran

- **The build.** Edwin's clone on FIOS-STB-01 was at `8663e5c` with no local
  changes. It was pulled, 21 commits, to `5a291c4`
  (`5a291c45bac4b2e72fdc35cfaa36bfce8e62f3b8`), and `publish-release.ps1` made
  a folder in Edwin's profile with SDK 10.0.401 (`global.json` lets a later
  feature band build): 216 files, 82,462,363 bytes, holding the service and
  the probe. The service's stamp is
  `0.4.0+5a291c45bac4b2e72fdc35cfaa36bfce8e62f3b8`: the version number has not
  changed since 0.4.0, and the commit follows the `+`. Its runtime
  configuration names `Microsoft.NETCore.App 10.0.12`. It is not signed.
- **The runtime.** Microsoft's release metadata for .NET 10, read through the
  fetch tool on 2026-10-22, gives 10.0.13 of 2026-10-13 as the latest release,
  with SDK 10.0.402, and marks it `"security": true`, as it does 10.0.12 of
  2026-09-08. Release 0.4.0 carries 10.0.13
  ([finding](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md)).
- **The install.** One command, run by Edwin in an SSH session on FIOS-STB-01,
  checked the new folder's stamp and that the printer answered on port 631
  before stopping anything; stopped the service; replaced
  `C:\Program Files\SecretPrinter` with the new folder; checked the installed
  stamp before starting the service again; and printed the service's lines
  since the stop. Release 0.4.0 logged `Service 'SecretPrinter' stopping.` at
  21:33:39Z, then `No longer accepting print jobs on Ethernet.`,
  `Advertisement retracted (goodbye records sent).`, its stop summary and
  `SecretPrinter stopped.`. The
  summary counted 225 queries served of 22,570 seen (IPv4 216 of 15,228, IPv6
  9 of 7,342); since when it had counted was not read. The build's first line,
  at 21:33:42Z, was its stamp. It found the printer at once, found no other
  device answering for its names, and was accepting print jobs on its IPv4 and
  link-local addresses at 21:33:45Z. The printer was not offered for 5 to 7
  seconds.
- **The settings that bear on this,** from the build's start-up lines: resolve
  timeout 5 s, connect timeout 10 s, the printer interface the Wi-Fi.

## How it was read

Read-only commands, run by Edwin in the SSH session and pasted, each printing
the service's lines since a given time with link-local and other IPv6
addresses, the printer's host name and UUIDs replaced, and the advertisement's
own record listings left out.

- **The first read after the test** printed every line since 21:44:21Z. The
  paste stopped partway through a line at 21:52:48Z. One pasted line also had
  a piece of the command's own text joined to it; the service cannot write that
  text, so it came from the copy, not the log.
- **The second** printed every line that is not a `Job:` line in full, counted
  the `Job:` lines by kind in three periods (before the switch-off, while the
  printer was off, after it was switched on), counted the failure reasons and
  the client addresses, and printed how each connection after 22:08Z ended.
  Claude ran it first, in its workspace, against a log built from the first
  paste, and its counts matched a count by hand.
- **The third,** for the page after the test, printed the same for lines since
  22:23:09Z. It was run twice; the first time, at 22:29:53Z, came before the
  page was printed.

## The page before the test

Edwin printed one page from his iPhone; it came out at 5:43 pm (21:43Z). The
log, read at 21:44:28Z and again later, shows 14 connections from 21:40:24Z, all
from an IPv6 link-local address and none over IPv4. Each was located from the
resolver's cache (`cached, 0s`), relayed with `encryption to printer: TLS 1.2,
certificate matched the pinned fingerprint`, and completed; the last ended at
21:46:35Z. None failed and none was refused. The page went in the connection
opened at 21:42:09Z, which carried 1,067,612 bytes to the printer in 139.23
seconds. The page's connection on 2026-10-09 carried 1,065,220
([finding](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md)).

## The test

| Time | What |
| --- | --- |
| 21:50:06Z to 21:52:11Z | Five connections, all completed, carrying 657 to 12,483 bytes to the printer. |
| 5:52 pm (21:52Z) | Edwin switched the printer off. Claude had asked for its power button; how it was switched off was not recorded. |
| 21:52:15Z to 21:52:59Z | Eleven connections accepted and located from the cache. Every one failed: `Could not connect to the printer at 192.168.12.180:631: No TCP connection to 192.168.12.180:631 was established within 10s.` |
| 21:53:39Z | `Printer unreachable: it stopped answering on the printer-side interface and its records have expired. …`, `No longer accepting print jobs on Ethernet.`, `Advertisement withdrawn and the listener closed: …` |
| 21:53:39Z to 22:08:40Z | Nothing logged. |
| 6:08 pm (22:08Z) | Edwin switched the printer on. |
| 22:08:40Z | `Printer reachable again: it was heard on the printer-side interface without being asked (an announcement, or its answer to another device).` and `Probing for the advertised names before offering them (RFC 6762 s8.1).` |
| 22:08:41Z | `Probe clear: …`, and `Accepting print jobs on …:631 (Ethernet)` for the IPv4 address and for the link-local address. |
| 22:08:43Z | `Advertisement restored: the printer is on offer again.` |
| 22:13:54Z | `Printer unreachable: …`, `No longer accepting print jobs on Ethernet.`, `Advertisement withdrawn …` |
| 22:18:51Z | `Printer reachable again: it answered on the printer-side interface.` and `Probing …` |
| 22:18:52Z | `Probe clear: …`, and `Accepting …` for both addresses. |
| 22:18:54Z | `Advertisement restored: the printer is on offer again.` |
| 22:21:34Z to 22:33:02Z | Six connections, all completed. One was held 687.82 seconds and carried 226,796 bytes to the printer; the others 657 to 2,102. |
| 22:25:32Z to 22:34:52Z | Fourteen connections, all completed, among them the page below. |
| 6:34 pm (22:34Z) | Edwin printed one page; it came out. |

Edwin's words: "off 5:52 on 6:08"; "no i checked supply levels but i didnt
print anything except that one page. i did not touch the printer"; "printed at
6:34 page came out". When and how he checked supply levels was not recorded,
so nothing here ties it to particular connections.

The reads that counted client addresses found one in each, link-local, and
none over IPv4: from 21:44:21Z to 22:23:11Z, and from 22:23:09Z to 22:43:56Z.
Apart from the eleven above, no connection failed and none was refused.

## Against what a run had to show

1. **The line, and the advertisement restored after it: shown, once.** The
   line came at 22:08:40Z and the advertisement was restored at 22:08:43Z, no
   more than about 40 seconds after Edwin switched the printer on. Only what
   the resolver hears while no question of the service's is waiting can
   produce that line; an answer to a question, the service's or a print
   job's, produces `it answered`. Worked out from the code below, the
   service's questions were then 8½ minutes apart: the last had ended at about
   22:03:00Z and the next was due at about 22:11:32Z. **Not shown: "within
   seconds of the printer's first announcement."** No listener ran, so when the printer first announced
   itself, and whether what was heard was an announcement or the printer's
   answer to another device, is not known.
2. **No such line while the printer is held reachable: not exercised on a
   network.** No second such line came between 22:08:40Z and the loss at
   22:13:54Z. On 2026-10-21 the printer went on announcing for about two
   minutes after its first announcement, but whether it did so today, and
   whether FIOS-STB-01 received it, is not shown. The tests show it
   (`PrinterWatchTests` and `PrinterReachabilityTests`, "… while it is held
   reachable changes nothing").
3. **A page afterwards: shown.** Printed at 6:34 pm; the connection opened at
   22:33:34Z carried 1,070,407 bytes to the printer in 77.68 seconds, and the
   page came out.

Two things the finding of earlier today listed under "What this does not show"
are now shown, once each:

- **That FIOS-STB-01 receives the printer's records between questions.** The
  test of 2026-10-21 heard them on the development machine.
- **That the service still hears after its printer-side socket is
  reopened.** Before each question to a printer held unreachable the service
  reopens that socket (`REQ-RES-009`), so what was heard at 22:08:40Z came
  through the socket opened for the question of about 22:02:55Z. Until today
  only a test showed it (`PrinterSideTests`, "Every resolver made here hands
  the printer's announcements on, after a reopen too").

## The arithmetic

From the code (`PrinterReachability`, `PrinterWatch`,
`ServiceHost.AskForWatchAsync`): when the printer is lost, the service asks at
once; a question that goes unanswered waits out the 5-second resolve timeout;
the next comes 1 second after that, then 2, 4, 8 seconds and so on, doubling
up to an hour. So the questions after a loss begin 0, 6, 13, 22, 35, 56, 93,
162, 295, 556 and 1,073 seconds after it.

- **From the loss at 21:53:39Z** the tenth question began at about 22:02:55Z
  and ended at about 22:03:00Z, before the printer was switched on, and the
  eleventh was due at about 22:11:32Z. No question was waiting at 22:08:40Z.
  Release 0.4.0 asks on the same schedule; it would have found the printer at
  about 22:11:32Z, if the printer answered then. Not run.
- **From the loss at 22:13:54Z** the ninth question began at about 22:18:49Z.
  The service logged `it answered` at 22:18:51Z. That is the one check of the
  arithmetic in this run, and it held to within 2 seconds.
- **Before Edwin switched the printer on,** Claude had asked for 12 to 14
  minutes off, so that the next question would come 4 to 8 minutes after the
  switch-on. At about 16 minutes it came 1 to 5 minutes after, which Claude
  told Edwin before the log was read. The line's wording, not its time, is
  what tells the two paths apart.

## The second loss

At 22:13:54Z the service lost the printer again, 5 minutes 14 seconds after it
had heard it. The log records changes only, so the rest is worked out from the
code:

- A record runs out 120 seconds after the answer that gave it, the TTL the
  printer gives. So the service's last answer came at about 22:11:53Z, and its
  four reconfirmations of that answer, begun at 80 to 97% of its lifetime
  (about 22:13:29Z to 22:13:49Z), went unanswered. Had the four
  reconfirmations after 22:08:40Z gone unanswered, the record would have run
  out at about 22:10:40Z. Only an answer to the first of them, at about
  22:10:17Z, puts the next answer at about 22:11:53Z, so both of those
  questions were answered.
- After the loss, the questions of 22:13:54Z to about 22:16:41Z, eight of
  them, went unanswered, and the ninth, at about 22:18:49Z, was answered.
- Nothing was heard from the printer between 22:13:54Z and 22:18:49Z: no
  `heard … without being asked` line came.
- No `The printer-side interface is usable again` line came, so the service's
  examination of the Wi-Fi adapter, every 5 seconds, saw no change while it
  held the printer unreachable. A change shorter than 5 seconds may not be
  seen.
- Edwin did not touch the printer. No print was tried while it was withdrawn.

**Why the printer went unanswered is not shown:** the log cannot say whether
the printer did not answer or its answers did not reach FIOS-STB-01. Nothing in
the log ties it to the change: a fault in the reader would have been logged,
and none was, and an answer that arrives while a question is waiting goes to
that question as it did before the change. On 2026-10-21, after a restart of
about 40 seconds, the printer answered every question the listener saw in the
13 minutes after it came back
([finding](2026-10-21-the-printer-announces-itself-when-it-starts.md)). A loss
that was not a loss of the link is on record before: the longest in [the run
of 2026-10-11](2026-10-11-the-start-over-ran-overnight-on-fios-stb-01.md).

## Connections while the printer was off

The eleven that failed, from 21:52:15Z to 21:52:59Z, came in the minute Edwin
switched the printer off, by his time, and before the service withdrew it at
21:53:39Z, 40 to 99 seconds after the switch-off. They are the problem README
Known problems describes: "For a short time after the printer stops answering,
it is still offered, and a print in that time fails." Edwin printed nothing
then. What made them, and the connections at 21:50Z, 22:21Z and 22:25Z, is not
shown.

## What Claude predicted, and what it did not

Held: every line of the build; the install but for its gap; for the page
before the test, the connections from a link-local address and none over IPv4,
each relayed with TLS 1.2 and the pin matched, none failed or refused, no
warnings and no reachability lines; the loss between about 20 seconds and 2
minutes after the switch-off; nothing logged from the withdrawal until the
printer came back; the `heard … without being asked` line and the restore
after it, line for line;
the first period's count exactly (22 lines: 5 accepted, 5 located, 5 relaying,
7 completed); no connection after the withdrawal; every failure `Could not
connect`; no IPv4; for the page after the test, no reachability lines, no
failures and a connection of about a megabyte. Said in advance to be unknown:
the number of connections, their sizes, whether lookups were cached, and how
long the printer would take to start.

Not held:

- **The install's gap.** Claude said the printer would be unavailable for 7 to
  10 seconds. It was 5 to 7.
- **"Each has four lines,"** for the page before the test: three connections
  had not ended when the log was read. Each had its `completed` line by
  21:46:35Z. Among them was the page's, which Claude had told Edwin could not
  yet be identified; it ended at 21:44:28Z, the second the log was read.
- **The connections at 21:50Z and the eleven that failed.** For the time before
  the loss Claude named only the `completed` lines of the three connections
  still open.
- **"No `Job:` lines unless someone printed,"** after the printer came back:
  six connections came from 22:21:34Z, before Edwin's second page, and he
  printed nothing else.
- **"Nothing after that,"** after the restore: the second loss.
- **"The rest carry a few thousand bytes or less,"** for the page after the
  test: one carried 226,796 bytes and another 12,614.
- **The first read printed more than the copy carried.** Claude's command
  printed every line; the second read summarised.

## What this does not show

- **The release build.** This build was made on FIOS-STB-01 and is not signed.
- **When the printer first announced itself,** or that what was heard was an
  announcement and not its answer to another device. No listener ran.
- **On a network, that no line comes while the printer is held reachable.**
- **Why the service's questions went unanswered from about 22:13:29Z to
  22:16:41Z,** or whether the printer announced itself when it answered again;
  nothing was heard before the question of about 22:18:49Z was answered.
- **A long outage,** with the questions an hour apart, or a printer that comes
  back without being switched on, such as when its own Wi-Fi reconnects.
  *(Status note, 2026-10-25, by Claude, Claude Opus 5.5: one loss of 4 hours
  10 minutes came on 2026-10-25, with the questions an hour apart. The printer
  was found by a question, not heard, and when it came back is not shown; see
  the status note at the top.)*
- **What made the connections** that were not Edwin's two pages.
- **How far apart the two clocks were.**

## Raised, not decided

- **What FIOS-STB-01 runs.** This build, unsigned and one security release
  behind, or release 0.4.0 again, which does not hear the printer; and when to
  release 0.5.0, which would carry `REQ-RES-011` and a current runtime. These
  are Edwin's to decide. *(Status note, 2026-10-25, by Claude, Claude Opus 5.5:
  on 2026-10-25 Edwin chose to release 0.5.0, "Release 0.5.0 (Recommended)",
  over keeping this build and going back to 0.4.0.)*
- **Whether to look into the second loss,** and how. Edwin's to decide.
- The items raised in [the finding of earlier
  today](2026-10-22-the-service-hears-the-printer-announce-itself.md) stand.

## How it was measured

- **The build, the install and the reads** by the commands above, run by Edwin
  West in an SSH session on FIOS-STB-01 and pasted. Claude checked each command
  with PowerShell's parser before sending it. The filters replaced addresses,
  the printer's host name and UUIDs before printing.
- **Edwin's times and words,** as he gave them.
- **The arithmetic** by hand, from the code named above, in Claude's notes; not
  in this repository.
- **Durations** from the logged times, by subtraction.
- **Microsoft's release metadata,** through the fetch tool.

## What changed

- This finding.
- `README.md`: the note on `REQ-RES-011`; under Known problems, the entry on
  the printer-side network, which said `REQ-RES-011` "has not been run on a
  network", and the section's opening note.
- `docs/operating.md`: under "When the printer-side network drops", in "Coming
  back can be slow", the words "it has not been run on a network" replaced, and
  a note on the change.
- Status notes on the findings of
  [earlier today](2026-10-22-the-service-hears-the-printer-announce-itself.md),
  [2026-10-21](2026-10-21-the-printer-announces-itself-when-it-starts.md),
  [2026-10-10](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md)
  and [2026-10-03](2026-10-03-the-printer-came-back-at-an-hourly-question.md),
  each of which said that `REQ-RES-011` had not been run on a network.
