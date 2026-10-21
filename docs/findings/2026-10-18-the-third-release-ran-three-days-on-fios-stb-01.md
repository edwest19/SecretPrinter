# The third release ran three days on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-18. Reviewed by a human before merge.*

**Status: measured, once, by reading logs; nothing was run, restarted or
changed. Release 0.3.0 ran on FIOS-STB-01 from its start at 17:16:48Z on
2026-10-15 to a reading at 19:27:20Z on 2026-10-18, 74 hours 10 minutes,
without a restart of the service or of Windows. It accepted 145 connections
and relayed and completed all 145, each with the printer's certificate
matching the pinned fingerprint; none failed and none was refused. It lost the
printer 34 times and found it again each time without a restart. One loss
lasted 10 hours 14 minutes, while the printer did not answer on its own
network; Edwin West restarted the printer that evening, at a time not
recorded. The printer-side Wi-Fi went down seven times, and each time a script
of Edwin West's, not Windows, connected it again. While the printer was not
answering, that script's tests of the printer's port took the default route
out the client network at all 62 of its runs: the seventh and eighth
occasions under the fifth point of README "Read this before installing". No
code changed.**

*(Status note, 2026-10-19, by Claude, Claude Opus 5.5: the script's whole log,
from 2026-10-09 to 2026-10-19, was read on 2026-10-19. All 1,224 of its tests
that succeeded went through the printer-side adapter; of the 220 that failed,
213 took the default route, 212 of them while the service was not offering the
printer. That record now stands in README in place of the seventh and eighth
occasions this finding added. See [the finding of that
reading](2026-10-19-ten-days-of-port-tests-and-three-windows-settings-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
Windows' event log or from the commands run on FIOS-STB-01. Times in am or pm
are US Eastern, UTC-4, as the script logs them. 2026-10-15 was a Thursday,
2026-10-16 a Friday and 2026-10-17 a Saturday.

## What was read

Four read-only commands, run by Edwin in his SSH session on FIOS-STB-01 on
2026-10-18 between 3:27 and 3:50 pm, and pasted or attached:

1. The service's log from 17:16:42Z on 2026-10-15, the second the build
   0.3.0 replaced stopped: counts of its lines, losses, starts, stops and
   connections, and every line about a loss, a restore, the printer-side
   adapter, a warning or an error; then every line of the script's log since
   1:16 pm on 2026-10-15 saying it found the Wi-Fi not up or a ping not
   answered.
2. Windows' WLAN-AutoConfig operational log since 1:16 pm on 2026-10-15,
   each event's first line with its adapter, connection mode and reason; and
   the script's whole runs from 2:30 to 3:20 pm on 2026-10-17.
3. The service's `Printer lookup failed` lines, and the result of each of the
   script's tests of the printer's port since 1:16 pm on 2026-10-15.
4. For each of those tests that did not succeed, the adapter and source
   address `Test-NetConnection` reported.

## The service

- **Windows last started at 20:55:31Z on 2026-10-14,** so it did not restart
  in these three days. The service was running, and the program's stamp read
  `0.3.0+064ca9fc7cf649ff3eed615758274bedb533767f`. Its log holds one start in
  the period, 0.3.0's at 17:16:48Z on 2026-10-15, and one stop, that of the
  build it replaced, in the second before.
- **Connections:** 145 accepted, 145 relayed with the printer's certificate
  matching the pinned fingerprint, 145 completed; none failed and none was
  refused. The log holds no line at the level `ERROR`.
- **Losses:** 34 withdrawals and 34 restores. Each withdrawal was logged in
  the same second as `Printer unreachable:`, and each restore 3 seconds (32
  times) or 2 seconds (twice) after `Printer reachable again:`. The printer
  was not offered for 10 hours 57 minutes 42 seconds in all, 14.8% of the
  74 hours.
  - One loss lasted 10 hours 14 minutes 27 seconds (below). The other 33
    lasted 43 minutes 15 seconds together, the longest of them 9 minutes 21
    seconds, from 06:09:31Z to 06:18:52Z on 2026-10-17.
  - 19 of the 34 lasted 17 seconds or less. In 8 of them `Printer reachable
    again:` came in the same second as `Printer unreachable:`, and the printer
    was offered again 3 seconds later. By the code (`PrinterReachability`),
    the service asks again at once when it holds the printer unreachable,
    after reopening its printer-side socket (`REQ-RES-009`); which question
    was answered is not in the log. Why the reconfirmations before each loss
    went unanswered is not established; no capture ran.
  - Four losses came with the printer-side adapter not up (below). The other
    30 came with no line about the adapter.
- **Warnings:** apart from the lines of the losses, the log holds 23
  `Printer lookup failed` lines (below) and nothing else.
- **The log** held 993 lines from 17:16:42Z on 2026-10-15; its last, at the
  reading, was from 18:51:24Z on 2026-10-18.

## The printer-side Wi-Fi

Windows' WLAN-AutoConfig log holds seven connections for the printer-side
adapter in these three days. **All seven are "Manual connection with a
profile", and each began within about a second of a run of the script logging
that it was connecting.** None is automatic. As on 2026-10-14 and 2026-10-15, Windows did
not connect this adapter by itself.

| Went down (Windows) | How | Connected again (Windows) | Down for | The service |
| --- | --- | --- | --- | --- |
| 02:50:01.628Z, 10-16 | disconnected "by the driver", after "Dynamic key exchange did not succeed within configured time" | 02:50:29.894Z | 28 s | withdrew the printer at 02:50:35Z, was answered in that second, offered it at 02:50:38Z |
| 08:28:09.973Z, 10-16 | disconnected "by the driver", after the link's security stopped and restarted repeatedly from 08:27:46Z | 08:30:30.210Z | 2 min 20 s | withdrew it at 08:29:38Z, the adapter holding its address but not up; usable again at 08:30:34Z; offered at 08:30:37Z |
| 10:46:58.057Z, 10-16 | disconnected "by the driver", after the same key-exchange failure | 10:50:29.129Z | 3 min 31 s | withdrew it at 10:47:41Z, the adapter holding its address but not up; usable again at 10:50:32Z; answered at 10:51:30Z; offered at 10:51:33Z |
| 15:09:53.734Z, 10-16 | disconnected "by the driver", after the same key-exchange failure | 15:10:29.399Z | 36 s | withdrew it at 15:14:11Z, 3 min 42 s after the reconnect; offered it at 15:14:20Z |
| 17:15:24.807Z, 10-16 | "Wireless security stopped", then nothing until the reconnect | 17:20:29.476Z | 5 min 5 s | withdrew it at 17:16:11Z, the adapter holding only an IPv4 link-local address; usable again at 17:20:30Z; the printer did not answer until 03:30:35Z on 10-17 (below) |
| 18:40:47.846Z, 10-17 | disconnected "by the user": the script's own `netsh wlan disconnect` | 18:40:52.633Z | 5 s | nothing logged; the printer stayed offered |
| 19:02:31.870Z, 10-17 | "Wireless security stopped", then nothing until the reconnect | 19:10:29.786Z | 7 min 58 s | withdrew it at 19:02:39Z, the adapter with no IPv4 address; usable again at 19:10:34Z; offered at 19:10:37Z |

"Down for" runs from the time in the first column to Windows' "successfully
connected". Where the service logged `The printer-side interface is usable
again:`, it did so within 5 seconds of that, and asked the printer at once
(`REQ-RES-010`). Windows also logged, 24 times in the three days, the link's
security stopping, starting again and succeeding within a fraction of a
second, with no disconnect.

**The script** is `Check-SP.ps1`, which runs every 10 minutes as a scheduled
task; it is not part of SecretPrinter and is not in this repository (see the
[finding of 2026-10-14](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md)).
Its run at 2:40 pm on 2026-10-17 found the Wi-Fi up, sent four pings to the
gateway of the printer's network from 2:40:29 pm and logged at 2:40:47 pm
that none was answered; it then ran `netsh wlan disconnect`, waited 5
seconds, ran `netsh wlan connect`, and its test of the printer's port
succeeded through the Wi-Fi. That changes the host's network configuration,
which the service does not do (`REQ-SEC-007`); it is Edwin's choice for his
machine.

In three of the seven the service did not withdraw the printer while the
Wi-Fi was down. In the sixth, of 5 seconds, it did not withdraw it at all. In
the first and fourth, of 28 and 36 seconds, it withdrew it only after the
reconnect, 5 seconds and 3 minutes 42 seconds after it. Why the fourth came
that late is not established.

## The lookups that failed

All 23 `Printer lookup failed` lines read `SocketException: The requested
address is not valid in its context.`, and the service counted each as no
answer, as its code says. 13 came from 17:15:49Z to 17:20:27Z on 2026-10-16
and 10 from 19:02:35Z to 19:06:55Z on 2026-10-17: the two drops after which
Windows logged only "Wireless security stopped" and the adapter lost its
address. Four of the first group and one of the second came before the loss,
while the service still held the printer reachable and was reconfirming it;
the rest were the question asked at once after the loss and the next eight,
each interval about double the one before, from 1 second to 128
(`REQ-RES-008`). In the two other drops that withdrew the printer, the adapter
kept its address, and the questions went unanswered without an error.

## The loss of 10 hours 14 minutes

- **1:15:24 pm on 2026-10-16:** Windows logged "Wireless security stopped" for
  the printer-side adapter. From 17:15:49Z the service's questions failed as
  above, and at 17:16:11Z its records of the printer expired: it withdrew the
  printer and could not reopen its printer-side socket, the adapter holding
  only an IPv4 link-local address and not up.
- **1:20:29 pm:** the script found the Wi-Fi disconnected and connected it.
  At 17:20:30Z the service found the adapter usable, reopened its socket and
  asked the printer at once (`REQ-RES-010`). The printer did not answer.
- **From 1:20 pm to 11:20 pm,** at each of the script's 61 runs, its test of
  the printer's port failed, each by the default route (next section). At
  none of those runs after the first did the script find the Wi-Fi not up,
  and at none did it log its pings to the gateway unanswered.
- **Edwin, on 2026-10-18:** "i restarted the printer once yesterday when it was
  not answering on either network for a long time but i dont remember when".
  Asked whether that could have been Friday evening, during this loss, he
  answered "Yes, Friday evening".
- **11:30:35 pm (03:30:35Z on 2026-10-17):** the service logged `Printer
  reachable again:`, and it offered the printer again at 03:30:38Z. The
  script's run of 11:30 pm tested the port successfully, in a test that ended
  at 11:30:45 pm; that of 11:20 pm had failed.

So the printer's port began answering after the test of 11:20 pm and by
11:30:45 pm, and the service offered the printer again at 11:30:38 pm, no
more than about ten minutes after the port began answering. When within those
minutes it began is not known. By the code
(`REQ-RES-008`), the service's questions had been about an hour apart since
about 2:30 pm, a little over an hour after the start over, and the question
that found the printer fell in those ten minutes. When Edwin restarted the
printer, and whether the restart is what brought it back, are not recorded.

## The script's port tests, and the default route

The script tests TCP port 631 on the printer with `Test-NetConnection` at
every run. It ran 447 times from 1:16 pm on 2026-10-15 to the reading: 384
tests succeeded and 63 did not.
*(Status note, 2026-10-19, by Claude, Claude Opus 5.5: the counts by day read
on 2026-10-19 give 445 runs begun in that time, 64, 144, 144 and 93, all on
the schedule. Which count is right was not settled. The 63 failures agree with
that reading.)*
*(Status note, 2026-10-21, by Claude, Claude Opus 5.5: settled by one read-only
count of the script's log on 2026-10-21. From 1:16 pm on 2026-10-15 to the
reading at 3:27:20 pm on 2026-10-18 (19:27:20Z), the script began 445 runs and
logged 445 port results: 63 failed, all of them listed below, and 382
succeeded. The 447, and with it the 384 here and below, came from a later
command of the same session, run at about 3:46 pm with no end time, which also
counted the runs begun at 3:30:28 and 3:40:28 pm; both came after the last
failure listed below, at 2:10 am on 2026-10-17, and succeeded. The 63 failures
are unchanged.)*

- **62 of the 63 reported the interface `Ethernet` and the machine's
  client-side address as their source:** Windows chose its default route, out
  the client network, for the printer's address. 61 were the runs from 1:20 pm
  to 11:20 pm on 2026-10-16, in the long loss above. The other was at 2:10 am
  on 2026-10-17, in the loss of 9 minutes 21 seconds; at that run too the
  script found the Wi-Fi up and logged no unanswered ping, and the run of
  2:20 am succeeded.
- **One failed through the Wi-Fi,** from the printer-side address, at 6:10 am
  on 2026-10-16, while the service held the printer reachable and offered it
  (it had last found it at 10:03:15Z and next lost it at 10:47:41Z). The next
  run's test succeeded. Why the printer's port did not answer that test is
  not established.
- **Which adapter the 384 tests that succeeded used was not read,** except at
  the six runs read in full, from 2:30 to 3:20 pm on 2026-10-17, each of which
  used the Wi-Fi.
  *(Status note, 2026-10-19, by Claude, Claude Opus 5.5: read on 2026-10-19,
  every test of the script that succeeded, from 2026-10-09 to 2026-10-19, went
  through the printer-side adapter.)*

So in these three days every test of the script that failed while the service
held the printer unreachable took the default route, and the one that failed
while it held the printer reachable took the printer-side adapter. Why Windows
chooses the default route with the printer-side adapter up is still not
established. These are the seventh and eighth occasions under the fifth point
of README "Read this before installing", which said six until this finding.
The script's log before 1:16 pm on 2026-10-15 was not read for this.
*(Status note, 2026-10-19, by Claude, Claude Opus 5.5: it has since been read;
see the status note at the top.)*

None of this was the service's connection. The service accepts no connection
while the printer is withdrawn (`REQ-LIF-006`). Whether any of the 145
connections was open, or reaching for the printer, when a loss began was not
read.

**A miss in README found while writing this.** The paragraph on step 5 in
section 3, Architecture, said Windows had been measured choosing the default
route on four occasions. The fifth and sixth, measured on 2026-10-14, were
added that day to the fifth point of "Read this before installing" but not to
that paragraph. Both now say eight.

## What Claude predicted, and what it did not

Held: no restart of Windows or of the service; the stamp; one start and one
stop, the stop being the replaced build's; withdrawals equal to restores, and
between 20 and 60 of them; every connection relayed with the pinned
fingerprint, completed, and none failed or refused; every connection Windows
logged being a manual one, each from a script run; a disconnect within 10
minutes before the script's two reconnects that the service did not see; the
four losses with the adapter down coming within about 2.5 minutes of the drop
(7 to 88 seconds); the script's run at 2:40 pm on 2026-10-17 disconnecting and
reconnecting the Wi-Fi with no loss; the lookup failures all `SocketException`
and all while the Wi-Fi was down; about 445 runs of the script; its port test
failing from 1:20 pm on 2026-10-16 and succeeding again at a run between
10:40 and 11:30 pm that day. The number of lookup failures and of the script's
reconnects was said in advance to be unknown, and so was the adapter the
failed port tests used.

Not predicted:

- **The log's size:** Claude said 1,500 to 3,500 lines since the install, from
  the rate of the build before, whose period held two start-ups; there were
  993. And that its last line would be from the last few minutes; it was 36
  minutes old.
- **A loss with the Wi-Fi down:** Claude said such a loss would be withdrawn
  without a `Printer unreachable:` line. Every one of the 34 has that line,
  those four included.
- **A disconnect before every reconnect:** in two of the seven drops Windows
  logged only "Wireless security stopped", as it had on 2026-10-12 and
  2026-10-14.
- **The failed port test at 6:10 am on 2026-10-16,** through the Wi-Fi, while
  the printer was offered.
- **Before the port tests were read,** Claude told Edwin that the printer may
  have been back for up to an hour before the service found it. The tests put
  it at no more than about ten minutes.

## What this does not show

- **Updating from one release to the next** by the document's steps.
  *(Status note, 2026-10-20, by Claude, Claude Opus 5.5: shown that day on
  FIOS-STB-01, where release 0.4.0 replaced release 0.3.0 by the steps under
  "From a release", with the signature check
  ([finding](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md)).)*
- **A Windows shutdown or restart with 0.3.0 running,** or a start of 0.3.0 at
  boot: Windows did not restart in these three days.
- **0.3.0 on Windows 11, or on a machine with no .NET.**
- **Why 30 of the losses came with the printer-side adapter usable:** whether
  the printer was silent or its answers were not heard. No capture ran.
- **When the printer came back on 2026-10-16** to better than the script's ten
  minutes, and when it was restarted.
- **Which devices made the 145 connections,** or how many pages they carried.

## How it was read

The four commands above, given to Edwin and run by him, each read-only, in an
SSH session on FIOS-STB-01. They masked link-local and other IPv6 addresses,
IPv4 link-local addresses, hardware addresses, ids, the printer's host name
and the printer-side network's name, the last read from the script's own
setting. The machine's addresses on both networks, and the printer's, are in
what was pasted and are not repeated here. Figures such as the total time not
offered and the counts by length were worked out in Claude's workspace from
the pasted lines.
