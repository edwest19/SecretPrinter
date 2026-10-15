# The service stopped cleanly at a Windows restart

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-14. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-14 a build of commit `8663e5c`, which
corrects how the service stops when Windows shuts down or restarts (README
`REQ-LIF-009`; [finding](2026-10-14-a-windows-restart-ended-the-service-before-it-stopped.md)),
was made on FIOS-STB-01 and installed there as the service, and Windows was
restarted with it running while the printer was offered. At the shutdown the
service logged that Windows was shutting down, stopped accepting print jobs,
retracted the advertisement with goodbye records, wrote its summary and logged
`SecretPrinter stopped.`, all within the same second, 20:49:12Z. Windows
recorded no application error. Windows then restarted a second time by itself,
to finish installing an update, while the service was waiting at startup for
the printer-side Wi-Fi, and that stop ended as the code says it should too.
After the second boot the service waited until the Wi-Fi was connected again,
15 minutes later, by a script of Edwin West's, and was accepting print jobs
within 8 seconds of that. The goodbye was logged; it was not captured on the
wire. No code changed.**

No IPv6 address, hardware address or device name appears in this finding, and
neither does any machine's address on either network, nor the name of either
network. The printer's host name is not given.

**About the times.** Times ending in `Z` are UTC, from FIOS-STB-01's service
log and from Windows' event logs, as Edwin West read them with commands given
to him. Times in am or pm without a `Z` are US Eastern, UTC-4 that day, from
the log of Edwin's script and from the commands, which use local time.

## Why this was done

The finding that came with the correction measured it against the code, and on
Windows by a test that calls the shutdown handler as .NET's `ServiceBase` does.
It said what a run at a real restart would need to show: the service's log
holding, before the boot, `stopping: Windows is shutting down.`, the goodbye
line, the summary and `SecretPrinter stopped.`; and no application error from
`SecretPrinter.Service.exe` in the System log. After the correction was pushed,
what came next was put to Edwin as one question. Claude recommended the
restart run; Edwin chose it ("Restart run now"), on FIOS-STB-01, where the
service runs day to day.

## The build and the install

Made on FIOS-STB-01 by `publish-release.ps1`, from its clone fast-forwarded
from `c0992a5` to `8663e5c`, clean afterwards, with .NET SDK 10.0.401. Both
programs carry `Microsoft.NETCore.App` 10.0.12. 216 files, 82,448,343 bytes,
stamp `0.2.0+8663e5c6fa2ba97b163bff318dbe1ee17e3ad466`. Unsigned, and not made
by the release workflow.

It was installed as the service in place of the build of `c0992a5`, which had
run since 16:40:53Z ([finding](2026-10-14-seventeen-answers-waited-at-random-on-a-network.md)).
That build was stopped at 20:31:24Z and wrote its summary and `SecretPrinter
stopped.`: 4 queries served of 1,130 seen, 1,117 for other services, 9
carrying every answer already, none held back within a second, none refused
for its address, none answered by unicast because it asked for it, 3 answered
after the random wait; IPv4 answered 0 of 327 seen and IPv6 4 of 803.
The program folder was replaced by the new build's 216 files, its stamp was
read back, and the service was started. It started at 20:31:27Z, its probe was
clear at 20:31:29Z, and at 20:31:31Z it had announced and was accepting print
jobs.

## The restart

At 20:42:41Z `Restart-Computer` was refused: Windows answered that the shutdown
could not be initiated because other users were logged on to the computer.
`query user` showed Edwin's own account signed in at FIOS-STB-01's console.
Asked, Edwin chose `Restart-Computer -Force` over restarting from the console.
It returned without error, and Windows recorded the request at 20:44:32Z
(below). The console session was signed out. The SSH session stayed up until
about 20:49Z.

## What the service logged

From the service's log, read after the second boot. Lines beginning `publish`,
`DROP`, `record` or `nsec`, and the indented lines that describe the
configuration, were left out of the reading, as in earlier readings; long lines
are cut here at `...`:

```
2026-10-14 20:48:30Z  INFORMATION  Job: accepted from [(link-local address)]:52386.
2026-10-14 20:48:30Z  INFORMATION  Job: accepted from [(link-local address)]:52387.
2026-10-14 20:48:31Z  INFORMATION  Job: relaying [(link-local address)]:52387 -> ...; encryption to printer: TLS 1.2, certificate matched the pinned fingerprint.
2026-10-14 20:48:31Z  INFORMATION  Job: relaying [(link-local address)]:52386 -> ...; encryption to printer: TLS 1.2, certificate matched the pinned fingerprint.
2026-10-14 20:49:12Z  INFORMATION  Service 'SecretPrinter' stopping: Windows is shutting down.
2026-10-14 20:49:12Z  INFORMATION  No longer accepting print jobs on Ethernet.
2026-10-14 20:49:12Z  INFORMATION  Advertisement retracted (goodbye records sent).
2026-10-14 20:49:12Z  INFORMATION  Served 6 quer(ies) of 122 seen; 95 were for other services and were ignored; 21 carried every answer already and were not answered (RFC 6762 s7.1); 0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 3 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 6 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
2026-10-14 20:49:12Z  INFORMATION  By transport: IPv4 answered 3 of 29 seen; IPv6 answered 3 of 93 seen.
2026-10-14 20:49:12Z  INFORMATION  SecretPrinter stopped.
2026-10-14 20:50:58Z  INFORMATION  Service 'SecretPrinter' starting under the service control manager.
2026-10-14 20:50:58Z  INFORMATION  SecretPrinter starting.
2026-10-14 20:50:58Z  INFORMATION  Print job data passes through this machine. See README.md.
2026-10-14 20:50:58Z  WARNING      Waiting for the printer-side interface: Interface 'Wi-Fi' holds (an IPv4 link-local address) but is not up. ...
2026-10-14 20:55:00Z  INFORMATION  Service 'SecretPrinter' stopping: Windows is shutting down.
2026-10-14 20:55:00Z  INFORMATION  Stopped while waiting at startup. Nothing had been offered to the client network, so there is nothing to retract.
2026-10-14 20:56:38Z  INFORMATION  Service 'SecretPrinter' starting under the service control manager.
2026-10-14 20:56:38Z  INFORMATION  SecretPrinter starting.
2026-10-14 20:56:38Z  INFORMATION  Print job data passes through this machine. See README.md.
2026-10-14 20:56:38Z  WARNING      Waiting for the printer-side interface: Interface 'Wi-Fi' holds (an IPv4 link-local address) but is not up. ...
2026-10-14 21:10:38Z  INFORMATION  The printer-side interface 'Wi-Fi' is usable. No longer waiting for it.
2026-10-14 21:10:38Z  INFORMATION  Probing for the advertised names before offering them (RFC 6762 s8.1).
2026-10-14 21:10:39Z  INFORMATION  Probe clear: no other device answered for the advertised names.
2026-10-14 21:10:41Z  INFORMATION  Announced. Answering queries.
2026-10-14 21:10:42Z  INFORMATION  Accepting print jobs on ...:631 (Ethernet); permitted clients: ...
```

Between the usable line and the probe at 21:10:38Z the log holds the sockets
being opened and joined, and the printer's answers to the service's first
questions, in the same second; they are left out above.

- **The stop at the first restart is complete.** Every line the earlier finding
  asked for is there, before the boot: `stopping: Windows is shutting down.`,
  the goodbye line, the summary and `SecretPrinter stopped.`. No `Shutdown did
  not finish within 20s`, no `Stopped without completing shutdown cleanly`, no
  warning and no error. All six lines carry the same second, 20:49:12Z; the
  log's times are to the second, so how much of that second the stop took is
  not known.
- **The advertisement was on offer** at the restart, so the goodbye had
  something to retract: connections were relayed until 20:48:31Z, and the stop
  logged that it no longer accepted print jobs and had sent the goodbye.
- **The summary** covers the time the printer was offered since the start at
  20:31:27Z, at most 17 minutes 41 seconds; the log from 20:31:31Z to 20:44:18Z
  was not read. 6 queries served, every one after the random wait
  (`REQ-ADV-029`), 3 of them by unicast because they asked for it
  (`REQ-ADV-028`); 21 carried every answer already (`REQ-ADV-026`); none held
  back within a second (`REQ-ADV-027`); none refused for its address
  (`REQ-SEC-017`). The counts add up: 6 + 95 + 21 + 0 + 0 = 122 seen;
  29 + 93 = 122; 3 + 3 = 6 answered.
- **The connections.** From 20:44:21Z to the stop, 16 connections were accepted
  and relayed, each with the printer's certificate matching the pinned
  fingerprint; 14 of them completed, none failed and none was refused. They
  carried between 662 and 27,268 bytes towards the printer and lasted from 0.9
  to 109.85 seconds. What they carried is not in the log, which records no
  content (`REQ-PXY-009`), and no print was reported for that time. **The last
  two, accepted at 20:48:30Z, were open at the stop and have no ending line.**
  That is the gap stated under `REQ-PXY-009` on 2026-10-07: a connection the
  service itself ends is not reported. It is another sighting, not a new
  problem.
- **The second stop came during the startup wait,** and ended as the code says
  a stop there ends: the shutdown line, then `Stopped while waiting at
  startup...`, and no summary or `SecretPrinter stopped.`, which the code does
  not write on that path. Release 0.2.0 ended the same way at a stop request
  that morning, at 16:08:22Z
  ([finding](2026-10-14-seventeen-answers-waited-at-random-on-a-network.md)).
  This is the case the Windows test in `ShutdownTests.cs` builds, here at a
  real shutdown.

## What Windows logged

The System log from 4:44 pm Eastern, for the events asked for (1074, 6005,
6006, 6008, 7011, 7031, 7034, 7043, 41, 12, 13 and 26):

| Time (UTC) | Event | Source | What it says |
|---|---|---|---|
| 20:44:32.727 | 1074 | User32 | `wmiprvse.exe` has initiated the restart of FIOS-STB-01 on behalf of Edwin's account; reason "No title for this reason could be found", code 0x80070015 |
| 20:49:11.945 | 7034 | Service Control Manager | the OpenSSH SSH Server service terminated unexpectedly |
| 20:49:12.508 | 6006 | EventLog | the event log service was stopped |
| 20:49:20.516 | 13 | Kernel-General | the operating system is shutting down |
| 20:49:44.819 | 12 | Kernel-General | the operating system started (at 20:49:44.500) |
| 20:50:17.539 | 6005 | EventLog | the event log service was started |
| 20:54:28.689 | 1074 | User32 | `TrustedInstaller.exe` has initiated the restart of FIOS-STB-01 on behalf of `NT AUTHORITY\SYSTEM`; reason "Operating System: Upgrade (Planned)", code 0x80020003 |
| 20:54:44.761 | 7034 | Service Control Manager | the OpenSSH SSH Server service terminated unexpectedly |
| 20:55:00.741 | 6006 | EventLog | the event log service was stopped |
| 20:55:09.649 | 13 | Kernel-General | the operating system is shutting down |
| 20:55:33.815 | 12 | Kernel-General | the operating system started (at 20:55:33.500) |
| 20:56:03.477 | 6005 | EventLog | the event log service was started |

**No application error (event 26) at either shutdown,** and none of the events
read names SecretPrinter. That morning's restart, under release 0.2.0, had one,
from `SecretPrinter.Service.exe`, with the code of a .NET exception.

## Why Windows restarted twice

The Setup log holds two packages changed to the Installed state after the
second boot, KB5126657 at 20:56:46.738Z and KB5131595 at 20:56:56.825Z (events
2, Servicing), and the System log, at 21:07:16.146Z, "Installation Successful:
Windows successfully installed the following update: 2026-10 Cumulative Update
for Windows 10 Version 22H2 for x64-based Systems (KB5126657)" (event 19,
WindowsUpdateClient). So the restart that was asked for also finished
installing that update, and the second restart, by `TrustedInstaller.exe`, was
the update's own. What KB5131595 is, its event does not say, and it was not
looked up. FIOS-STB-01 runs Windows 10 22H2, build 19045, as before; the SSH
session's banner afterwards gave 10.0.19045.7807. Both stops came before the
packages were recorded as installed.

4 minutes 40 seconds passed between the request, 20:44:32Z, and the stop,
20:49:12Z, and the service accepted and relayed connections throughout. That
morning about 2 minutes 15 seconds passed between Windows' own request and the
service's `stopping`. Whether the time went on the update is not established:
nothing read times any work before 20:49:12Z.

## After the restarts: the printer-side Wi-Fi

After both boots the printer-side Wi-Fi was not connected. The service logged
it holding only an IPv4 link-local address and not up, and waited, offering
nothing (README `REQ-LIF-008`). At 21:05:43Z Windows reported it
`Disconnected`.

**The WLAN-AutoConfig operational log,** read from 4:30 am Eastern, records
for the printer-side adapter "Wireless security stopped" at 20:49:13.747Z, at
the first shutdown, and then nothing until 21:10:34.640Z, when a connection
started, "Manual connection with a profile"; it succeeded at 21:10:34.752Z. No
failed attempt is recorded after either boot, and none at the second shutdown,
when there was nothing connected to stop. Every connection that log holds from
4:30 am is a manual connection with a profile; none is automatic.

**Edwin's script `Check-SP.ps1`** has run on FIOS-STB-01 every 10 minutes since
2026-10-09, as a scheduled task. It is not part of SecretPrinter and is not in
this repository; by its own header, Claude wrote it in another conversation,
at Edwin's request. It reconnects the printer-side Wi-Fi when Windows does not
report it up, disconnects and reconnects it when the gateway does not answer a
ping, and tests TCP port 631 on the printer. This changes the host's network
configuration, which the service does not do (`REQ-SEC-007`). That is Edwin's
choice for his machine.

Its log, read from 4:44 pm Eastern:

- **No run until 5:10:29 pm.** That run found the Wi-Fi `Disconnected` and ran
  `netsh wlan connect`, which answered "Connection request was completed
  successfully." with exit code 0 at 5:10:34 pm, the second the WLAN log
  records the connection starting. The gateway then answered, and port 631 on
  the printer answered through the Wi-Fi.
- **Its runs at 5:20, 5:30 and 5:40 pm** found the Wi-Fi up and connected
  nothing; port 631 answered each time.
- **Its runs fall at about 28 seconds past each tenth minute.** None is logged
  for 4:50 pm, when the machine was starting after its first restart, or for
  5:00 pm, after the second boot, when the Wi-Fi was not connected. Why the
  5:00 pm run is absent is not established.

So the script connected the Wi-Fi again, 15 minutes 1 second after the second
boot. Windows did not connect it by itself between the first boot and the
second restart, or in the 15 minutes after the second boot.

## The service came back

At 21:10:38Z, 4 seconds after the connection, the service logged the
printer-side interface usable. The printer answered its first questions in the
same second, the probe was clear at 21:10:39Z, the service announced at
21:10:41Z and accepted print jobs from 21:10:42Z, 8 seconds after the
connection. No warning or error.

## How long the household had no printer

- **21 minutes 30 seconds,** from the stop at 20:49:12Z to 21:10:42Z: the two
  restarts and the wait for the Wi-Fi.
- **1 minute 38 seconds** later the same evening, below.

## Also seen

- **The printer stopped answering for about a minute and a half.** At
  21:28:52Z the service logged the printer unreachable, its records expired,
  stopped accepting print jobs and withdrew the advertisement (`REQ-LIF-006`).
  At 21:30:27Z the printer answered again; the probe was clear and print jobs
  accepted at 21:30:28Z, and the advertisement was restored at 21:30:30Z. The
  run of Edwin's script at 5:30:28 pm (21:30:28Z) began after the printer had
  answered. Why the printer went quiet is not established. The WLAN log shows,
  at 21:26:42Z, the Wi-Fi's security stopping and starting again within 50
  milliseconds with no disconnect, as at four other times that day (12:58:41Z,
  15:48:22Z, 20:12:43Z and 20:36:48Z); whether that bears on it is not
  established.
- **A port test after the restart left by the client adapter.** At 21:05:43Z,
  with the printer-side Wi-Fi disconnected, `Test-NetConnection` to the
  printer's port 631 went out by the client-side Ethernet and was not answered.
  As the first time recorded under the fifth point of "Read this before
  installing", the printer-side adapter had no link. The README counts it
  there as the sixth occasion.
- **The OpenSSH SSH Server service** was recorded as terminated unexpectedly
  (event 7034) at both shutdowns, as at 08:38:15Z that morning. It is not
  SecretPrinter's, and it was not examined.
- **Earlier that day the printer-side Wi-Fi dropped twice:** at 09:33:31Z, 20
  seconds after a connection at 09:33:11Z, connected again at 09:52:24Z; and at
  20:13:05Z, while the build of `c0992a5` ran, connected again at 20:20:31Z.
  Each of those connections was a manual connection with a profile. The last
  falls on the script's schedule; the morning's two do not, and what made
  them is not established. The script's log for those times, and the
  service's log around 20:13Z, were not read.

## What Claude predicted, and what it did not

Each command was given with a prediction. These did not hold:

- **The time Windows took to begin the shutdown,** 4 minutes 40 seconds, was
  not predicted. Edwin, before the shutdown began: "i dont think it
  restarted. how can i tell".
- **The boot time,** said as between about 20:49Z and 20:55Z: it was 20:55:33Z,
  because of the second restart, which was not predicted.
- **The update's entries,** said as between 20:44Z and 20:56Z if the second
  restart was finishing an update: they came at 20:56:46Z, 20:56:56Z and
  21:07:16Z.
- **An event in the WLAN log at each shutdown:** there was none at the second,
  because the Wi-Fi had not been connected in between.
- **Before the WLAN log was read, Claude said that on FIOS-STB-01 nothing
  reconnects the printer-side Wi-Fi until a person does,** from the finding of
  [2026-09-20](2026-09-20-the-wlan-drops-and-nothing-retries.md). Edwin's
  script has done so since 2026-10-09, as the findings of 2026-10-10 and
  2026-10-11 record, and Claude had read its log that morning. Edwin corrected
  it: "we do have a scheduled task that runs to bring up the wi-fi."
- **The printer's silence at 21:28:52Z** and **the OpenSSH events** were not
  predicted.

These held: the refusal of `Restart-Computer` while a user was signed in, named
beforehand as a possible outcome; the six stop lines; no application error;
the stamp and the service running after the boot; the service offering the
printer within seconds of the Wi-Fi coming back; and the script's 5:10 pm run
connecting it and its later runs connecting nothing.

## What this does not show

- **The goodbye on the wire,** or a client dropping the printer at the restart.
  No capture was running, and the iPhone's list was not looked at.
- **A shutdown that arrives while a stop request is under way,** or the
  reverse.
- **How much of a second the stop took,** or how near it came to the 5 seconds
  of `WaitToKillServiceTimeout` on FIOS-STB-01.
- **A shutdown that turns the machine off.** Both were restarts.
- **A release.** Neither 0.1.0 nor 0.2.0 carries the correction; this was a
  build made on FIOS-STB-01. *(Status note, 2026-10-15, by Claude, Claude Opus
  5.5: release 0.3.0, published on 2026-10-15, carries it, and replaced this
  build on FIOS-STB-01 that day; a restart with the release running has not
  been seen yet. See [the finding of the
  release](2026-10-15-the-third-release-was-published-and-installed-on-fios-stb-01.md).)*
- **Why** Windows took 4 minutes 40 seconds, why the script's 5:00 pm run is
  absent, why Windows did not connect the Wi-Fi after a boot, and why the
  printer went quiet at 21:28Z.

## How it was read

Read-only commands, run by Edwin in an SSH session on FIOS-STB-01 and pasted or
attached. Link-local and other IPv6 addresses, the printer's host name, UUIDs,
hardware addresses and the printer-side network's name were replaced before
printing. The machines' IPv4 addresses were printed, and so was, in the first
reading of the service log, the printer-side adapter's IPv4 link-local
address; none of them is given here. The service log was read from its last
400 lines and then its last 2,000; the
System and Setup logs and the WLAN-AutoConfig operational log with
`Get-WinEvent`, the last keeping each event's first line and its adapter,
connection-mode and reason lines; the script's log from 4:44 pm Eastern.

## Left on the machines

- **FIOS-STB-01:** the build of `8663e5c` installed as the service, offering
  the printer when last read, since 21:30:30Z; its clone at `8663e5c`; the
  build folders `sp-c0992a5` and `sp-8663e5c`. The folder of release 0.2.0,
  kept from its install, is the way back. Windows carries the October
  cumulative update. Edwin's script runs as before.
- **The development machine and the test machine:** not used for the run.
