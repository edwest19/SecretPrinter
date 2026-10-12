# Four answers were held back within a second on a network

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-12. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-12 a build of commit `9a5d0ab`, which
keeps a record from being multicast twice within a second on one client adapter
(README `REQ-ADV-027`;
[finding](2026-10-12-a-record-is-multicast-at-most-once-a-second.md)), offered
the printer for 9 minutes 59 seconds from the test machine, in place of
FIOS-STB-01's service. An iPhone printed a page through it. At its stop the
service reported 116 queries seen: 4 answered, 80 for other services, 28 that
carried every answer already, and 4 that were not answered because their answer
would have repeated records multicast less than a second before. That last
count exists only since `9a5d0ab`, and under the code before it those 4 would
have been answered. The household had no printer for 3 minutes 35 seconds. No
code changed.**

No IPv6 address, hardware address or device name appears in this finding, and
neither does the test machine's address on either network. The configuration's
UUID, the printer's own and the printer's certificate fingerprint are not
given.

**About the times.** Times ending in `Z` are UTC and come from the machines'
logs and the commands' output. Times in pm are US Eastern, by Edwin West's
account.

## Why this was done

The finding that came with the change measured it against the code only, with
a fake transport, and listed what a run on a network would need to show: the
new count above zero after an iPhone has browsed for printers while the service
ran, and a page printed through it.

Where and when to run it was Edwin's to decide. Four ways were put to him: the
test machine from a console, that day, with FIOS-STB-01's service stopped but
not changed; the same another day; the build on FIOS-STB-01 overnight in place
of release 0.2.0; or no run until the next release. Claude recommended the
first, because it is how the change before this one was run on 2026-10-10
([finding](2026-10-10-questions-carrying-their-answers-went-unanswered-on-a-network.md)),
it leaves the household's server on the signed release, and the test machine
had seen many questions over IPv4, which is where the cost the change states
would show. Edwin chose it: "win11dev now (Recommended)".

## The build

- **Made on the development machine** at `9a5d0ab`, with nothing uncommitted,
  by `publish-release.ps1` into a new folder outside the repository, with .NET
  SDK 10.0.303: 216 files, 82,254,341 bytes, the service and the probe each
  carrying `Microsoft.NETCore.App 10.0.11`. Its stamp read
  `0.2.0+9a5d0ab2f36ac29b7794a5c7512727c60508187b`. It says 0.2.0 because the
  version has not been changed since that release; the commit after the `+`
  tells the two apart.
- **Not the release build.** It was not signed, it did not come from the release
  workflow, and it carries .NET 10.0.11 where release 0.2.0 carries 10.0.12.
- **Moved to the test machine** as a zip of 36,168,426 bytes. Its SHA-256 on the
  test machine,
  `6EBFFF191AFF8C5A23AACC0C9FD2B77996429029FEE2FF92074791C1FCE50708`, was the
  one read on the development machine. Unpacked into a new folder: 216 files,
  the same stamp, the same runtime.

## The run

| Step | Machine | What happened |
| --- | --- | --- |
| Firewall | test machine | No rule named a SecretPrinter program. Two were added, as `operating.md` gives them (UDP 5353 and TCP 631, inbound, Private profile) but naming the test build's program and with "(9a5d0ab test)" in their names. |
| Networks | test machine | Edwin connected its Wi-Fi to the printer's network by hand. Both adapters up and Private, on the addresses and indexes recorded on 2026-10-10, and the printer's port 631 answered through the printer-side adapter. |
| Stop | FIOS-STB-01 | Running, its last change having been to offer the printer again at 18:50:35Z, and the printer answered on port 631. The service stopped at **21:32:51Z** and sent its goodbye. |
| Start | test machine | Run from a console window, not elevated, with the configuration made by hand on the test machine on 2026-10-08 and a log file of its own. Started 21:35:23Z; probe clear 21:35:24Z; announced and accepting print jobs on its IPv4 and link-local addresses at **21:35:26Z**. No warning and no error. |
| Print | iPhone | Edwin was asked to open the printer list, leave it open for about a minute, and then print one page. He tapped Print at 5:38 pm: "it started to print a few seconds after". |
| Stop | test machine | Ctrl+C. The stop lines are at **21:45:25Z**; the summary is below. |
| Start | FIOS-STB-01 | Started 21:46:22Z; probe clear 21:46:23Z; announced and accepting at **21:46:25Z**, running release 0.2.0 as before. No warning and no error. |
| Firewall | test machine | Both rules removed. No rule names a SecretPrinter program, and no SecretPrinter process runs. |

FIOS-STB-01's installed program, registration, configuration and firewall rules
were not touched.

From probe clear to `Announced` took 2 seconds, as on 2026-10-10; by the code
the three announcements are a second apart. Nothing in the log shows an
announcement waiting for the new rule; the log gives whole seconds only.

## What the test build reported at its stop

```
Served 4 quer(ies) of 116 seen; 80 were for other services and were ignored; 28 carried every answer already and were not answered (RFC 6762 s7.1); 4 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6).
By transport: IPv4 answered 1 of 58 seen; IPv6 answered 3 of 58 seen.
```

- **4 queries were held back by the new rule:** every record their answer would
  have carried had been multicast on the client adapter less than a second
  before. Under the code before `9a5d0ab` each of them would have been
  answered. The queries answered and the three counts after them add up to the
  queries seen, so no answer failed to send.
- **28 carried every answer already** and were not answered by the rule of
  `REQ-ADV-026`, against 26 in the run of 2026-10-10.
- **Which questions the 4 were, and over which transport they arrived,** is not
  known: questions are not logged, the transport line counts answers and not
  questions held back, and nothing was captured. So whether any of the 4 was the
  second copy of a question asked over both transports, the cost `REQ-ADV-027`
  states, is not known either.
- **58 queries were seen over each transport.** Whether they were the same
  questions sent twice is not known.
- **This line was written by the service's own stop path,** in
  `ServiceHost.RunAsync`, which no test runs, as on 2026-10-10.

## The iPhone, and the print

- **The list,** in Edwin's words: "the list was the same. know printers was
  secretprinter and after a few seconds epson et-3720 appeared". The printer is
  an ET-3760. No screenshot was taken, and what the list showed beyond his words
  is not recorded here.
- **The connections:** 25 accepted, 25 relayed and 25 completed, the last
  between 21:37:20Z and 21:40:43Z, all from the iPhone's link-local address, each to
  192.168.12.180:631. None failed and none was refused. Every connection
  accepted has its ending line, unlike one on 2026-10-10.
- **The page:** one connection carried 1,056,318 bytes to the printer and 1,624
  back in 6.2 seconds, ending at 21:39:00Z, which fits Edwin's tap at 5:38 pm.
  No other connection carried more than 21,943 bytes.
- **No client connected over IPv4.**

So the iPhone found the printer and printed through it with four answers held
back by the new rule. Whether it would have found the printer any sooner
without the rule, or whether any of the four was an answer it waited for, this
does not show.

## How long the household had no printer

- From FIOS-STB-01's stop at 21:32:51Z to the test build's first `Accepting
  print jobs` line at 21:35:26Z: 2 minutes 35 seconds.
- From the test build's stop at 21:45:25Z to FIOS-STB-01's first `Accepting
  print jobs` line at 21:46:25Z: 1 minute 0 seconds. FIOS-STB-01's start was run
  at 21:46:21Z, about a minute after the stop; on 2026-10-10 the same gap was 10
  seconds.
- **In all, 3 minutes 35 seconds.** The test machine offered the printer from
  21:35:26Z to 21:45:25Z.

## FIOS-STB-01's own stop, for comparison

Release 0.2.0 had run since 2026-10-11 at 20:40:00Z, 24 hours 52 minutes 51
seconds:

```
Served 236 quer(ies) of 13657 seen; 13341 were for other services and were ignored; 80 carried every answer already and were not answered (RFC 6762 s7.1).
By transport: IPv4 answered 170 of 10407 seen; IPv6 answered 66 of 3250 seen.
```

236, 13,341 and 80 add up to 13,657, and the two transports to 236 and 13,657.
Release 0.2.0 does not have the new rule, so how many of its 236 answers
repeated records it had multicast less than a second before, it had no way to
say.

## What Claude predicted, and what it did not

Every prediction made before a command held: the build's commit, SDK, runtime,
file count and stamp, and a byte count of about 82 million; the zip's hash on
the test machine, and the folder's files, stamp and runtime there; the
configuration's adapter names and the start of its UUID; no firewall rule
beforehand and the two afterwards; both adapters, their addresses and indexes,
Private, and the printer reached; FIOS-STB-01's state, its last change of
offering, and its six stop lines; the test build's start-up, with no warning or
error; that a page would print; every connection completed, none failed or
refused; the summary's shape; FIOS-STB-01's start-up; and the clean state at
the end. The counts in the summary, the link speeds and the zip's size and hash
were said in advance to be unknown. A `Stopping.` line at the console stop was
called possible; it did not appear, as on 2026-10-10.

Not predicted: **the second gap's length.** The start on FIOS-STB-01 was given
ready to run straight after the stop; it ran about a minute later.

## What this does not show

- **Which questions the 4 were,** over which transport, and whether any was one
  the iPhone needed answered.
- **That an announcement or a goodbye waited,** on a network. The log gives
  whole seconds.
- **FIOS-STB-01, Windows 10, or the release build.** The test machine runs
  Windows 11, and the build was unsigned and carried .NET 10.0.11.
- **More than one run,** one iPhone and ten minutes.
- **The other two RFC 6762 items** of the finding of 2026-10-07.

## Left on the machines

All Edwin's to keep or delete: on the development machine, the test build's
folder and its zip; on the test machine, the unpacked folder, the zip, and the
test build's log, which holds link-local addresses and the printer's host name
and does not go in this repository; and the test machine's Wi-Fi connection to
the printer's network.
