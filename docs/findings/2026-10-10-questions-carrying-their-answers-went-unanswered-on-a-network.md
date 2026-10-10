# Questions carrying their answers went unanswered on a network

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-10. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-10 a build of commit `c56d697`, which
added known-answer suppression (README `REQ-ADV-026`;
[finding](2026-10-10-a-question-that-carries-its-answer-is-no-longer-answered.md)),
offered the printer for 9 minutes 41 seconds from the test machine, in place
of FIOS-STB-01's service. An iPhone listed the proxy and printed a page through it. At its stop
the service reported 134 queries seen: 8 answered, 100 for other services, and
26 that carried every answer already and were not answered. That last count
exists only since `c56d697`, and under the code before it those 26 would have
been answered. The household had no printer for 2 minutes 52 seconds. No code
changed.**

No IPv6 address, hardware address or device name appears in this finding, and
neither does the test machine's address on either network. The printer's host
name is written `EPSON000000`, and the configuration's UUID and the printer's
certificate fingerprint are not given.

**About the times.** Times ending in `Z` are UTC and come from the machines'
logs and the commands' output. Times in pm are US Eastern, by Edwin West's
account or his iPhone's clock.

## Why this was done

The finding of 2026-10-10 that came with the change measured it against the
code only, with a fake transport. Edwin decided on 2026-10-07 that each of the
four RFC 6762 items is built with a run on hardware, and that finding listed
what a run would need to show: the new count above zero after an iPhone has
browsed for printers while the service ran, and a page printed through it.

Where to run it was Edwin's to decide. Three ways were put to him: the test
machine, from a console window, with FIOS-STB-01's service stopped but not
changed; FIOS-STB-01 itself from a console; or the test build installed on
FIOS-STB-01 in place of 0.1.0 and 0.1.0 put back afterwards. Claude recommended
the first, because it changes nothing on the household's server and every step
had been done before on the test machine. Edwin: "ok option 1".

## The build

- **Made on the development machine** at `c56d697`, with nothing uncommitted,
  by `publish-release.ps1` into a new folder outside the repository: 216 files,
  82,233,813 bytes, the service and the probe each carrying
  `Microsoft.NETCore.App 10.0.11`. Its stamp read
  `0.1.0+c56d6976b9994e4073722577b12f4459911a3229`. It says 0.1.0 because the
  version has not been changed since that release; the commit after the `+`
  tells the two apart.
- **Not the release build.** It was not signed, it did not come from the release
  workflow, and it carries .NET 10.0.11 where release 0.1.0 carries 10.0.12.
- **Moved to the test machine** as a zip of 36,160,057 bytes. Its SHA-256 on the
  test machine,
  `3897EBF4FC758838A8EC41B1F33CC70441529ADE7161F387609E043DA7C4988A`, was the
  one read on the development machine. Unpacked: 216 files, the same stamp.

## The run

| Step | Machine | What happened |
| --- | --- | --- |
| Firewall | test machine | No rule named a SecretPrinter program. Two were added, as `operating.md` gives them (UDP 5353 and TCP 631, inbound, Private profile) but naming the test build's program and with "(c56d697 test)" in their names. |
| Networks | test machine | Edwin connected its Wi-Fi to the printer's network by hand. Both adapters up and Private, and the printer's port 631 answered through the printer-side adapter. |
| Stop | FIOS-STB-01 | Both adapters up and the printer reached first. The service stopped at **17:09:51Z** and sent its goodbye. |
| Start | test machine | Run from a console window, not elevated, with the configuration made by hand on the test machine on 2026-10-08 and a log file of its own. Started 17:12:30Z; probe clear 17:12:31Z; announced 17:12:33Z; accepting print jobs on its IPv4 and link-local addresses at **17:12:33Z**. No warning and no error. |
| Print | iPhone | Edwin was asked to open the printer list, leave it open for about a minute, and then print one page. He printed one page, tapping Print at 1:16 pm: "yes it printed". |
| Stop | test machine | Ctrl+C at **17:22:14Z**. The stop summary is below. |
| Start | FIOS-STB-01 | Started 17:22:21Z; probe clear 17:22:22Z; announced and accepting at **17:22:24Z**, running 0.1.0 as before. |
| Firewall | test machine | Both rules removed. No rule names a SecretPrinter program, and no SecretPrinter process runs. |

FIOS-STB-01's installed program, registration, configuration and firewall rules
were not touched.

## What the test build reported at its stop

```
Served 8 quer(ies) of 134 seen; 100 were for other services and were ignored; 26 carried every answer already and were not answered (RFC 6762 s7.1).
By transport: IPv4 answered 4 of 67 seen; IPv6 answered 4 of 67 seen.
```

- **26 queries carried every answer the service would have given,** with at
  least half its TTL, and got no answer. Under the code before `c56d697` each of
  them would have been answered. The three counts add up to the queries seen,
  so no answer failed to send.
- **This line was written by the service's own stop path,** in
  `ServiceHost.RunAsync`, which the finding that came with the change recorded
  that no test runs. A console run goes through `ServiceHost.RunAsync` as the
  installed service does (`Program.cs`, `WindowsService.cs`), so the line has
  now been seen written at a stop, once.
- **Which questions the 26 were** is not known: their names and types are not
  logged, and nothing was captured. On 2026-10-02 the questions seen carrying a
  known answer were for the service's `PTR`
  ([finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md)).

## The iPhone, and the print

- **The list,** in Edwin's screenshot at 1:17 pm, not in this repository:
  `EPSON ET-3760 Series` under Known Printers and `SecretPrinter (ET-3760)` under
  Other Printers, with the note "SecretPrinter proxy. Capabilities read from the
  printer at 2026-10-10 17:12:30Z", the test build's start. The proxy was under
  Other Printers on 2026-10-08 too, when the test machine ran with the same
  configuration
  ([finding](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md)).
- **The connections:** 16, accepted from 17:14:43Z to 17:17:50Z, all from the
  iPhone's link-local address, each relayed to 192.168.12.180:631 with "TLS 1.2,
  certificate matched the pinned fingerprint". None failed and none was
  refused. The largest opened at 17:16:24Z, which fits Edwin's tap at 1:16 pm,
  and carried 35,361 bytes to the printer in 76.05 seconds. Which connection
  carried the page is not in the log.
- **One connection has no ending line.** Accepted at 17:16:59Z and relayed, it
  was still open at the stop five minutes later, and the log has no line for
  its end. That is the known problem stated with `REQ-PXY-009`. It was not
  predicted.
- **No client connected over IPv4.**

So the service still let the iPhone find the printer and print through it with
the change in place. That shows nothing about how an iPhone would fare if an
answer it needed were left out, because none was.

## How long the household had no printer

- From FIOS-STB-01's stop at 17:09:51Z to the test build's first `Accepting print
  jobs` line at 17:12:33Z: 2 minutes 42 seconds.
- From the test build's stop at 17:22:14Z to FIOS-STB-01's first `Accepting
  print jobs` line at 17:22:24Z: 10 seconds.
- **In all, 2 minutes 52 seconds.** The test machine offered the printer from
  17:12:33Z to 17:22:14Z.

## FIOS-STB-01's own stop, for comparison

Release 0.1.0 had run since 2026-10-09 at 17:39:26Z, about 23 hours 30 minutes.
Its stop summary, in the form it had before `c56d697`:

```
Served 330 quer(ies) of 14699 seen; 14369 were for other services and were ignored.
By transport: IPv4 answered 231 of 12407 seen; IPv6 answered 99 of 2292 seen.
```

330 and 14,369 add up to 14,699, so 0.1.0 answered every query that was its to
answer. How many of those carried their answer already, it had no way to say.

## What Claude predicted, and what it did not

Every prediction made before a command held: the build's file count, runtime
and stamp; the zip's hash on the test machine; no firewall rule beforehand and
the two afterwards; both adapters up and Private, and the printer reached;
FIOS-STB-01's six stop lines; the test build's start-up, with no warning or
error; that a page would print; the shape of the new stop summary; FIOS-STB-01's
start-up; and the clean state at the end. The counts in the summary were said
in advance to be unknown, and so was whether the iPhone would send any query
carrying its answer.

Not predicted:

- **The connection with no ending line,** above.
- **One line more in FIOS-STB-01's start-up reading.** The filter kept lines
  matching `Announced`, and PowerShell's `-match` ignores case, so it also kept
  a line containing "not announced". Nothing was hidden.
- **No `Stopping.` line at the console stop.** It had been called possible, not
  expected.

## What this does not show

- **Which questions the 26 were,** or whether any of them was for anything but
  the `PTR` records seen on 2026-10-02.
- **FIOS-STB-01, Windows 10, or the release build.** The test machine runs
  Windows 11, and the build was unsigned and carried .NET 10.0.11.
- **More than one run,** one iPhone and ten minutes.
- **A query whose known answers go on in further packets** (RFC 6762 §7.2), which
  `REQ-ADV-026` does not handle.
- **The other three RFC 6762 items** of the finding of 2026-10-07.

## Left on the machines

All Edwin's to keep or delete: on the development machine, the test build's
folder and its zip; on the test machine, the unpacked folder, the zip, and the
test build's log, which holds link-local addresses and the printer's host name
and does not go in this repository; and the test machine's Wi-Fi connection to
the printer's network.
