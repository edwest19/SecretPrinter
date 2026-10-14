# Seventeen answers waited at random on a network

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-14. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-14 a build of commit `c0992a5`, which
waits a random 20 to 120 milliseconds before an answer that other devices may
be sending too (README `REQ-ADV-029`;
[finding](2026-10-13-an-answer-others-may-send-too-waits-at-random.md)), was
made on FIOS-STB-01 and installed there as the service in place of release
0.2.0. It offered the printer from 16:08:29Z to 16:40:48Z. An iPhone printed a
page through it. At its stop it reported 831 queries seen and 17 answered, and
every one of the 17 was answered after a random wait. A capture on the client
adapter holds three of those answers, sent 62.5, 103.0 and 114.6 milliseconds
after their questions. It is the last of the four RFC 6762 items recorded on
2026-10-07 to run on a network. The same build was started again at 16:40:49Z
and is left running on FIOS-STB-01, by Edwin West's choice. Before the run the
printer had not been offered for about 17 hours: the service could not reach
it, and when it was looked at, an iPhone on the printer's own network could not
find it either. That is described below. No code changed.**

No IPv6 address, hardware address or device name appears in this finding, and
neither does any machine's address on either network, nor the name of either
network. The configuration's UUID, the printer's certificate fingerprint and
the printer's own note are not given. The capture is not in this repository.

**About the times.** Times ending in `Z` are UTC and come from the machines'
logs, the commands' output and the capture. Times in pm or am without a `Z` are
US Eastern, by Edwin West's account or the clock on the iPhone's screenshots.

## Why this was done

The finding that came with the change measured it against the code only, with a
fake transport and a clock the tests move by hand, and listed what a run on a
network would need to show: the new count above zero after an iPhone has
browsed for printers; a page printed through the service; and, if a capture is
taken on the client adapter only, the time from a browse to its answer. It also
said that how long `Task.Delay` waits on Windows for 20 to 120 milliseconds had
not been measured.

What came first, this run or the next release, was put to Edwin as one
question. Claude recommended the run, so that the release would carry all four
RFC items measured on a network. Edwin chose the run, on FIOS-STB-01 rather
than the test machine: no one else was using the printer that day, and he
offered FIOS-STB-01's clone for the build.

## Before the run: the printer was not answering

The first reading of FIOS-STB-01, at 15:43:42Z, showed release 0.2.0 running
and the printer withdrawn since **2026-10-13 22:43:24Z**, when the service
logged it unreachable. A read-only reading followed:

- **The printer-side Wi-Fi was joined and held its address** from the
  printer's network, and Windows routed to the printer through it. The port
  test of 15:43:42Z, a few minutes earlier, had left by the client adapter
  instead, as the README's fifth point under "Read this before installing"
  describes; it is the fifth such occasion recorded there. The log shows the
  Wi-Fi holding only a `169.254` address at 22:43:25Z, at the start at
  08:39:44Z, and at 09:33:40Z, and holding its address again later each time
  (22:50:30Z, 09:33:15Z, 10:07:41Z). Its state at 15:43:42Z was not read.
- **Windows had begun a restart of FIOS-STB-01 at 08:36:02Z** (event 1074,
  from `svchost.exe` on behalf of `SYSTEM`), and it booted at 08:38:56Z. The
  service started again at 08:39:44Z, waited for the printer-side interface,
  then for the printer, and had not found it. What the service logged across
  that restart is not part of this run and is not examined here.
- **Edwin put the iPhone on the printer's own network,** where the printer
  advertises itself with nothing of SecretPrinter's in the way, and the
  iPhone's print sheet found no AirPrint printers. The printer itself was not
  answering.

Edwin restarted the printer. At 16:05:35Z it answered on port 631 through the
printer-side Wi-Fi. Release 0.2.0 had not yet noticed: by then it asked about
once an hour (README `REQ-LIF-008`). That did not matter here, because the
build replaced it.

## The build

Made on FIOS-STB-01 by `publish-release.ps1`, from its clone fast-forwarded
from `5db9063` to `c0992a5` (`git pull --ff-only`, 37 files changed, the clone
clean afterwards), with .NET SDK 10.0.401. Both programs carry
`Microsoft.NETCore.App` 10.0.12, which Microsoft's release metadata, read that
day, gave as its latest .NET 10 runtime. 216 files, 82,447,783 bytes, stamp
`0.2.0+c0992a5ba76c0eeed197ff7068956174305c57ac`. Unsigned, and not made by
the release workflow.

## The run

Each step was given to Edwin with what it does, the machine, the printer and a
prediction:

- **16:08:22Z,** after guards found the new folder's stamp and the printer
  answering on port 631, release 0.2.0 was stopped. It was still in its startup
  wait, and its log ended as its code says such a stop ends: `Stopped while
  waiting at startup. Nothing had been offered to the client network, so there
  is nothing to retract.`, with no summary. The program folder was replaced by
  the new build, whose stamp was read back before it was started. The
  registration, the configuration, the data folder and the firewall rules were
  not changed; the rules follow the program's path, which did not change.
- **16:08:25Z** the build started; the probe was clear at 16:08:27Z, and at
  **16:08:29Z** it had announced and was accepting print jobs. No warning or
  error.
- **By 16:12:06Z** a capture was running, with Windows' Packet Monitor, on the
  client adapter only, keeping UDP port 5353 only: mDNS, and not the print
  connections, which on the client side carry the page itself.
- **The iPhone,** by Edwin's account and its screenshots: at 12:14 and 12:15 pm,
  still on the printer's network, its list and its print sheet showed the
  printer itself; it was then joined to the client network, where at 12:17 pm
  the list showed "SecretPrinter (ET-3760)" under Known Printers, with the note
  "SecretPrinter proxy. Capabilities read from the printer at 2026-10-14
  16:08:26Z"; at 12:18 pm it showed that and, below it, "EPSON ET-3760 Series"
  with the printer's own note. Edwin: "printed a page a 12:18". At 12:19 pm
  the print sheet named the printer "EPSON ET-3760 Series" and the phone showed
  one page printing.
- **16:22:52Z,** the capture was stopped: 83 packets, none dropped. The same
  SHA-256 was read on FIOS-STB-01 and on the copy Claude read.
- **16:40:48Z,** after guards found the installed stamp and the printer
  answering on port 631, the service was stopped, and wrote its
  summary; it was started again at once, on the same build, by Edwin's choice
  ("Keep c0992a5"). The probe was clear at 16:40:51Z, and at **16:40:53Z** it
  was accepting print jobs.

## What the build reported at its stop

```
Served 17 quer(ies) of 831 seen; 802 were for other services and were ignored; 12 carried every answer already and were not answered (RFC 6762 s7.1); 0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 11 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 17 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
By transport: IPv4 answered 11 of 673 seen; IPv6 answered 6 of 158 seen.
```

- **All 17 queries served were answered after a random wait.** By the rule of
  `REQ-ADV-029`, each answer held one of the shared PTR records or answered a
  query of more than one question.
- **11 were answered by unicast because they asked for it,** each to an address
  the check of `REQ-SEC-017` allowed. None was refused for its address.
- **12 carried every answer already** and were not answered (`REQ-ADV-026`);
  **none was held back within a second** (`REQ-ADV-027`).
- The queries answered and the four counts after them add up to those seen:
  17 + 802 + 12 + 0 + 0 = 831. The transports add up too: 673 + 158 = 831 seen,
  11 + 6 = 17 answered.
- The counts cover only the time the printer was offered (README
  `REQ-LIF-006`), 32 minutes 19 seconds.

## The capture

83 packets from 16:14:21.921Z to 16:22:45.823Z, from three devices: one that
announced records of its own, over both transports, from 16:14:21Z to
16:14:52Z; one that
asked every question about SecretPrinter, from 16:17:10Z, the minute of the
iPhone's 12:17 pm screenshot, and is most likely the iPhone, though the capture
alone does not name it; and FIOS-STB-01. Only the mDNS headers and the times
were read.

**The service sent three answers in the capture, each after a random wait:**

| Question arrived | What it asked | Answer sent | By | After |
|---|---|---|---|---|
| 16:17:38.367Z | three questions about the service's host name, each asking for a unicast answer | 16:17:38.470Z | multicast | **103.0 ms** |
| 16:17:40.167Z | two browse questions (PTR), each asking for a unicast answer | 16:17:40.282Z | unicast | **114.6 ms** |
| 16:19:51.380Z | the same three host-name questions | 16:19:51.442Z | multicast | **62.5 ms** |

- Each query asked more than one question, and the second answer holds a
  shared record, so each took the wait. Each time runs from the question's
  packet to the answer's, both as Packet Monitor recorded them on the client
  adapter: it is the random wait plus the time to receive the query, decide
  and send. All three fall within 20 to 120 milliseconds.
- **The second answer went by unicast,** to the querier, because both of its
  questions asked for that and the PTR record it answered with had been
  multicast on that adapter less than a quarter of its 4,500-second TTL before;
  the service had announced it at 16:08:29Z (`REQ-ADV-028`). It was made as the
  multicast one would be: identifier 0, no question, one answer (the PTR,
  TTL 4,500, without the cache-flush bit) and four additionals (SRV, TXT, A
  and AAAA). The findings record no unicast answer from the service in an
  earlier capture.
- **The first and third went by multicast** although their questions asked for
  unicast: by the rule of `REQ-ADV-028`, the address records had not been
  multicast on that adapter within a quarter of their 120-second TTL. Each held
  an NSEC record and the A and AAAA records.
- All three left with an IPv6 hop limit of 255.
- **11 browse queries from the same device carried the service's PTR record as
  a known answer** and were not answered (`REQ-ADV-026`).
- **No two queries that each took the wait arrived together,** so the cost
  `REQ-ADV-029` states, a later answer held behind an earlier one's wait, was
  not exercised.

## The connections

14 accepted, 14 relayed with the printer's certificate matching the pinned
fingerprint, 14 completed, 0 failed, 0 refused, all from IPv6 link-local
addresses. The first ended at 16:18:10Z, the last at 16:34:22Z. The page's
connection was the last to end: 386,986 bytes sent to the printer and 295,776
received, open for 971.8 seconds, so opened at about 16:18:10Z. The other 13
moved between 662 and 62,164 bytes towards the printer.

## The iPhone, and the print

The page printed at 12:18 pm, by Edwin's account, and the log shows its
connection relayed through the proxy. Through FIOS-STB-01's installation the
list has shown the proxy under Known Printers before
([`docs/operating.md`](../operating.md)); the screenshots show that again. In
the screenshot of 12:18 pm a row named "EPSON ET-3760 Series", with the
printer's own note, stood below the proxy's on the client network, and at
12:19 pm the print sheet named the printer that way while the page went
through the proxy. Why the iPhone shows either is not established.

## How long the household had no printer

- **17 hours 25 minutes 5 seconds,** from 2026-10-13 22:43:24Z to 16:08:29Z,
  while the service held the printer unreachable. The swap of builds,
  from 16:08:22Z, fell inside that time.
- **5 seconds** at the restart after the run (16:40:48Z to 16:40:53Z).

## Also seen

- **Three questions sent by unicast straight to FIOS-STB-01's port 5353 were
  not answered.** At 16:19:51.044Z the same device sent three queries, one
  question each, for the service's host name, each asking for a unicast
  answer, to FIOS-STB-01's link-local address. Nothing answered them. 335.8
  milliseconds later it asked the same three by multicast and was answered
  62.5 milliseconds after that. RFC 6762 §5.5 says: "When a Multicast DNS
  responder receives a query via direct unicast, it SHOULD respond as it would
  for "QU" questions, as described above in Section 5.4." (Read on 2026-10-14
  through the fetch tool from rfc-editor.org, in segments; not compared with
  the RFC by hand.) By the code, had the service acted on the three, it would
  have multicast its answers at once, and then, those records having been
  multicast less than a second before, answered the multicast question by
  unicast; it answered it by
  multicast. So it did not act on them. Whether they reached it is not
  established. The same was seen on 2026-10-01, and 36 times across the six
  runs of 2026-10-02 ([finding](2026-10-01-an-iphone-printed-over-ipv6-link-local.md);
  [finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md)),
  where one possibility is named: that Windows' own DNS Client, which also uses
  port 5353, receives them. Not tested. The README now states it under Known
  problems.
- **FIOS-STB-01 itself asked over mDNS for a reverse name of the printer's
  address,** ending in `.in-addr.arpa.local`: 10 packets, over both transports,
  from 16:20:35.647Z to 16:20:41.270Z. SecretPrinter's code holds no reverse
  lookup. What on FIOS-STB-01 asked is not established.
- **673 IPv4 queries were seen** over the 32 minutes 19 seconds, but none from
  another device crossed the client adapter in the capture's 10 minutes 46
  seconds; the IPv4 packets in it were the first device's announcements and
  FIOS-STB-01's own reverse questions. Where and when the IPv4 queries
  arrived is not established: the log holds no line for each query.
- **The page's connection stayed open 16 minutes 12 seconds** and received
  295,776 bytes from the printer, against 1,624 to 3,277 bytes received for the
  pages of 2026-10-11, -12 and -13. Not established why.

## What Claude predicted, and what it did not

Every prediction given before a command held, except:

- **The first reading of FIOS-STB-01:** the restart that morning, the printer
  withdrawn since the evening before, and the port test leaving by the client
  adapter were not predicted, and the time, said as about 15:3x UTC, was
  15:43:42Z. The path stopped for one read-only reading.
- **That reading:** Claude expected the printer-side Wi-Fi to hold no address
  from the printer's network and the route to the printer to go by the client
  adapter; it held its address, and the route went by the Wi-Fi. Claude also
  expected the stop at the restart to be logged with its summary and
  `SecretPrinter stopped.`; neither was in the log. That is not examined here.
- **The pull** printed, after the fetch, a second block of `Enumerating
  objects` to `Writing objects` without the `remote:` prefix, which Claude took
  to be Git packing the clone's objects on FIOS-STB-01. A pull sends nothing.
- **The capture's size,** said as a few hundred kilobytes to a few megabytes,
  from the rate the test machine saw on 2026-10-13; it was 20,360 bytes.
  FIOS-STB-01's own rate would have predicted it.
- **A `Stopping.` line at the stop,** which the findings of 2026-10-10 and
  2026-10-12 already record as not appearing at a clean stop: the code logs it
  only when a stop arrives as an exception.
- **IPv4 answered "probably 0",** generalized from the capture's window; it was
  11.
- **The page's line "near 16:18Z":** its connection opened then, and its line
  came at 16:34:22Z.

## What this does not show

- **How the wait is spread.** Three timed answers fall within 20 to 120
  milliseconds; three are too few to say more, and each time includes more
  than the wait.
- **An answer held behind another's wait,** which `REQ-ADV-029` says can go
  more than 120 milliseconds after it was asked. It did not happen here.
- **Which devices asked over IPv4,** and which were answered there, or by
  unicast: the capture saw one of the 11 unicast answers.
- **The IP TTL of an IPv4 answer sent by unicast** (README `REQ-ADV-013`). The
  one unicast answer captured was IPv6.
- **A legacy querier,** answered or refused.
- **A build made by the release workflow, or signed.** This one was made by the
  same script on FIOS-STB-01.
- **Why the direct unicast questions went unanswered,** why the iPhone listed
  the second row, why the page's connection stayed open, or what asked for the
  reverse name.

## Left on the machines

- **FIOS-STB-01:** the build of `c0992a5` installed as the service, accepting
  print jobs since 16:40:53Z; its clone at `c0992a5`; the build folder
  `sp-c0992a5`; the capture and its conversion, with a copy in Edwin's own
  documents, outside this repository. The folder of release 0.2.0, kept from
  its install and, by the record, unchanged since, is the way back. Packet
  Monitor stopped and unloaded, with no filter left.
- **The test machine and the development machine:** not used.
