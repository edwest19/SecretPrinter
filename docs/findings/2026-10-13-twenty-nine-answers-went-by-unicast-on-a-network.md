# Twenty-nine answers went by unicast on a network

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-13. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-13 a build of commit `70f01c3`, which
answers by unicast a question that asks for a unicast answer (README
`REQ-ADV-028`;
[finding](2026-10-13-a-question-asking-for-a-unicast-answer-gets-one.md)), and
sends an answer by unicast only to an address on the network the query arrived
from (README `REQ-SEC-017`;
[finding](2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md)),
ran on the test machine in place of FIOS-STB-01's service. It offered the
printer for about 13 minutes 26 seconds in two spells. An iPhone printed a page
through it. At its stop it reported 1,324 queries seen and 31 answered, and of
those 31, 29 were answered by unicast because they asked for it. It is the first
time the service has been seen sending anything by unicast on a network. Which
devices asked that way is not known. Between the two spells the test machine's
printer-side Wi-Fi was disconnected for about 1 hour 27 minutes; the service
withdrew the printer, logged the Wi-Fi usable again about 3 seconds after the
command that reconnected it, and offered the printer again 3 seconds after
that, as README `REQ-RES-010` describes. The household had no printer for 1 hour 29
minutes 22 seconds in all. No code changed.**

No IPv6 address, hardware address or device name appears in this finding, and
neither does the test machine's address on either network. The configuration's
UUID, the printer's own and the printer's certificate fingerprint are not given.

**About the times.** Times ending in `Z` are UTC and come from the machines'
logs and the commands' output. Times in pm are US Eastern, by Edwin West's
account.

## Why this was done

The finding that came with the change measured it against the code only, with a
fake transport, and listed what a run on a network would need to show: the new
count above zero after an iPhone has joined the client network, or woken, while
browsing for printers; and a page printed through the service.

Where and when to run it was Edwin's to decide. Four ways were put to him: the
test machine from a console, that afternoon, with FIOS-STB-01's service stopped
but not changed; the same with a packet capture; a single run after the fourth
RFC item is built; or another day. Claude recommended the first, as on
2026-10-10 and 2026-10-12, and said what it could not settle without a capture:
a count of 0 would not say why. Edwin chose the first.

## The build

Made on the development machine by `publish-release.ps1` from a clean clone at
`70f01c3`, with .NET SDK 10.0.303. Both programs carry `Microsoft.NETCore.App`
10.0.11; Microsoft's latest .NET 10 runtime that day was 10.0.12, by its release
metadata. 216 files, 82,260,925 bytes, stamp
`0.2.0+70f01c30ae30495b5ef20e6c68e0561a3b851645`. Zipped, 36,170,810 bytes; the
SHA-256 of the zip was the same on the test machine. Unsigned. 216 files, as
the build of 2026-10-12 had, and 6,584 bytes more.

## The run

The ten steps of 2026-10-12, each given to Edwin with what it does, the machine,
the printer and a prediction, in one sitting:

- **18:01:57Z,** FIOS-STB-01's service stopped, after its guard found the
  printer answering on port 631.
- **18:02:48Z,** the test build started from a console on the test machine,
  with the configuration made by hand on 2026-10-08 and the test machine's own
  adapters. The probe was clear at 18:02:49Z, and at **18:02:51Z** it had
  announced and was accepting print jobs. No warning or error.
- **18:05:23Z to 18:05:56Z,** the iPhone opened nine connections through it.
  Three completed normally.
- **About 2:05 pm,** the test machine's Wi-Fi, its link to the printer's
  network, was turned off and on again by hand. The step had asked for the
  iPhone's Wi-Fi to be turned off and on; Edwin turned the test machine's. The
  test machine has not been seen rejoining the printer's network by itself
  ([the finding of the afternoon of 2026-10-06](2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)
  records it disconnected after a restart until it was connected by hand), and
  it stayed disconnected. Claude's step had said which device to use and not
  which to leave alone.
- **18:05:59Z,** five open connections were ended from the printer's side,
  each logged as `Relay ended early while reading from the printer: … forcibly
  closed by the remote host.`
- **18:06:27Z,** a lookup found `adapter 'Wi-Fi' is not up`; the printer was
  held unreachable, a sixth connection failed for that reason, and **the
  advertisement was withdrawn**. The iPhone then showed "No AirPrint Printers
  Found".
- **About 3:24 pm,** a read of the running build's log showed the reason. Edwin
  chose to reconnect the test machine and go on, against Claude's
  recommendation to end the run and start FIOS-STB-01 again.
- **19:33:24Z,** about 3 seconds after the Wi-Fi was reconnected with `netsh
  wlan connect` (the command then waited 20 seconds and read the time,
  19:33:41Z), the build logged that the printer-side interface was usable again,
  reopened its printer-side socket and heard the printer, all in the same
  second; it probed, was accepting at 19:33:25Z, and **restored the
  advertisement at 19:33:27Z**.
- **Step 7 was given again,** for the iPhone only, and saying to leave the test
  machine alone. Edwin's report: "3:38, page printed. the list showed known
  printer epson and other printer secretprinter." Whether the iPhone's Wi-Fi
  was turned off and on that time, and what 3:38 is the time of, were not
  asked. The page's connection ended at 19:40:27Z: 586,174 bytes sent to the
  printer, 1,632 received, 73.28 seconds.
- **19:43:17Z,** the test build stopped (Ctrl+C), said goodbye and wrote its
  summary. **19:44:41Z,** FIOS-STB-01's service was started; at **19:44:45Z** it
  was accepting print jobs, on release 0.2.0, unchanged.

## What the test build reported at its stop

```
Served 31 quer(ies) of 1324 seen; 1259 were for other services and were ignored; 34 carried every answer already and were not answered (RFC 6762 s7.1); 0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 29 of those served were answered by unicast because they asked for it (RFC 6762 s5.4).
By transport: IPv4 answered 26 of 1253 seen; IPv6 answered 5 of 71 seen.
```

- **29 of the 31 queries served were answered by unicast because they asked
  for it.** Each was sent to an address the check of `REQ-SEC-017` allowed, on
  the network the query arrived from.
- **Which devices asked, and over which transport, is not known.** The log
  holds no line for each query, and nothing was captured. §5.4 says a querier
  sets the bit on the questions it sends on waking or joining a network; why
  29 of 31 answered queries asked for unicast is not established.
- **None was held back within a second**, against 4 of 116 on 2026-10-12. That
  fits the change: a record a question asks for by unicast alone, multicast
  less than a second before, now goes by unicast instead of being held back.
  Whether that is why there were none is not established.
- **None was refused for its address.**
- **The queries seen** count only while the printer was offered (README
  `REQ-LIF-006`), about 13 minutes 26 seconds: 1,324, against 116 in 9 minutes
  59 seconds on 2026-10-12. Why so many more is not established.
- The queries answered and the four counts after them add up to those seen:
  31 + 1,259 + 34 + 0 + 0 = 1,324.

## The connections

19 accepted, 18 relayed, 13 completed, 6 failed, 0 refused. Every one has an
ending line. The six that failed are the five cut at 18:05:59Z and the one that
could not locate the printer at 18:06:27Z. The ten after the restore all
completed, between 19:38:17Z and 19:40:27Z, the page's among them.

## The iPhone, and the print

By Edwin's account: the page printed, and the list showed the printer under its
own name in Known Printers and "SecretPrinter" in Other Printers. That is where
the list put them on 2026-10-08, with an installation on the test machine
([`docs/operating.md`](../operating.md)). Whether the iPhone's own questions
were among the 29 is not known: the page would have printed from multicast
answers too, as it did before this change.

## The start-over, on the test machine

README `REQ-RES-010`'s start-over had been seen only on FIOS-STB-01. Here, after
the Wi-Fi was reconnected by command, the line `The printer-side interface is
usable again` came about 3 seconds after the command, and the printer was
offered again 3 seconds after the line. Nothing in SecretPrinter reconnected the Wi-Fi,
by design (`REQ-SEC-007`).

## How long the household had no printer

- **54 seconds** from FIOS-STB-01's stop to the test build accepting
  (18:01:57Z to 18:02:51Z);
- **1 hour 27 minutes 0 seconds** while the test build had withdrawn the printer
  (18:06:27Z to 19:33:27Z);
- **1 minute 28 seconds** from the test build's stop to FIOS-STB-01 accepting
  (19:43:17Z to 19:44:45Z).

1 hour 29 minutes 22 seconds in all, against 3 minutes 35 seconds on
2026-10-12. The second stretch is the one the test did not intend.

## FIOS-STB-01's own stop, for comparison

Release 0.2.0, from 2026-10-12 21:46:22Z to 2026-10-13 18:01:57Z:

```
Served 69 quer(ies) of 6297 seen; 6220 were for other services and were ignored; 8 carried every answer already and were not answered (RFC 6762 s7.1).
By transport: IPv4 answered 51 of 3961 seen; IPv6 answered 18 of 2336 seen.
```

Its last offering change before the stop was a restore at 13:00:40Z that day.

## What Claude predicted, and what it did not

Every prediction given before a command held, except:

- **The time of the network reading,** "about 18:0x UTC"; it was 17:59:47Z.
- **"No AirPrint Printers Found"** was not predicted, and nothing before it
  said the test machine's Wi-Fi had been touched. Claude stopped the run for a
  read of the running build's log, which explained it, before saying anything
  about a cause; the question of whose Wi-Fi had been turned off was asked
  after the printer was back.
- **The test machine's Wi-Fi did not reconnect** when Edwin was asked to
  connect it by hand; a read of the adapter and of its saved networks came
  before the `netsh wlan connect` that did.
- **The page's size,** said as "about a megabyte"; it was 586,174 bytes.

## What this does not show

- **Which devices asked for unicast answers,** over which transport, or
  whether the iPhone did.
- **That the iPhone used a unicast answer.** The page printed; it would have
  with multicast answers alone.
- **The IP TTL of an IPv4 answer sent by unicast** (README `REQ-ADV-013`).
  Nothing was captured.
- **Whether the IPv6 source address of a received datagram carries its scope
  on Windows,** which the check of `REQ-SEC-017` relies on over IPv6. Of the 5
  answered over IPv6, how many went by unicast is not known.
- **A legacy querier,** answered or refused. None is known to have asked.
- **Anything about a run on FIOS-STB-01** or in a release build.

## Left on the machines

- **The development machine:** the build folder and its zip.
- **The test machine:** the build folder, its zip, the console log (which holds
  household values), and its Wi-Fi joined to the printer's network. The two
  firewall rules added for the run were removed, and no SecretPrinter process
  was running.
- **FIOS-STB-01:** release 0.2.0, unchanged, accepting print jobs since
  19:44:45Z.
