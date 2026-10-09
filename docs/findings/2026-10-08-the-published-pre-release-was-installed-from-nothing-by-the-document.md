# The published pre-release was installed from nothing by the document

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-08. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-08, on the test machine, starting with no
SecretPrinter on it, the published pre-release `0.1.0-rc.3` was downloaded with
a browser from the repository's Releases page, checked by both checks in
[`operating.md`](../operating.md), configured by hand from the example the
program prints, installed as a Windows service under `LocalService`, started,
and an iPhone printed through it. Before that good start, the service was made
to fail on purpose: another program held the print port on the client-side
address, and the service found the printer, announced, failed to open its
listener, said goodbye and ended its process within about three seconds, as the
code of 2026-10-07 says it should. Windows then showed the service stopped with
error 1067 and recorded event 7034, "terminated unexpectedly". Last,
SecretPrinter was uninstalled by the document's steps and the machine read
clean. Every step was the document's own command, run as printed with its
placeholders filled in, except where this finding says otherwise. The walk
found no fault in the code. It found five places where the document was wrong
or silent, and they are corrected in the commit that carries this finding. No
release exists; one pre-release does.**

No IPv6 address, MAC address or device name appears in this finding. The second
machine is called the test machine, as in the findings it follows. The
printer's host name is written `EPSON000000`. The folder the work was done in is
written `<walk folder>`, and the UUID in the configuration is not given.

**About the times.** Times ending in `Z` are UTC. Unless they are marked
"about", they come from a machine's own clock or a service's log. A time marked
"about" is that of the message in which Edwin sent the result, converted to
UTC; the machine did not record it. Times given in pm are US Eastern, and each
says whose clock it is. The walk ran from 19:36Z to 21:49Z, which is 3:36 pm
to 5:49 pm Eastern.

## Why this was done

Item 4 of the plan to release 0.1.0 was an install from nothing on a second
machine, followed from `operating.md` itself, from a signed build published the
way a release will be. `0.1.0-rc.3` was published for that on 2026-10-07
([finding](2026-10-07-the-release-workflow-published-a-pre-release.md)). These
had never been done:

- downloading a published file with a browser, and checking that copy;
- the two `New-NetFirewallRule` commands on a machine with no SecretPrinter
  rule;
- the example configuration as the program has printed it since 2026-10-07,
  with its UUID empty, and the program's refusal of it on Windows
  ([finding](2026-10-07-the-example-configuration-handed-every-user-the-same-uuid.md));
- the `Remove-Item` line added to the install step on 2026-10-07
  ([finding](2026-10-07-the-readme-was-read-against-the-code.md));
- a service that ends its process when a part of it fails after it has started,
  under the Windows service control manager
  ([finding](2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md));
- the section "What you will see on an iPhone", checked against an iPhone.

Before the first step, what would complete the walk was stated: the published
zip downloaded with a browser, checked, configured by hand, installed as a
service under `LocalService`, started, and printed through from an iPhone;
each thing above run once; and a finding recording every place where the
document and the machine disagreed. All of it was done.

## What "following the document" means here

Edwin ran every command. Claude gave them one at a time and said what it
expected before each.

- **The document's commands were run as printed,** with placeholders filled in:
  the version, the folder, the printer's address, `<release-folder>` and
  `<your>`.
- **The firewall commands** name `C:\path\to\SecretPrinter.Service.exe`. They
  were run naming `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`,
  the file the service would run, before that file existed.
- **Unpacking.** The document says "Unpack the zip into a new folder" and gives
  no command. `Expand-Archive` was used.
- **The probe and the certificate command** were run as printed, with their
  output caught and filtered so that the printer's host name and its IPv6
  addresses did not come into the conversation. The certificate command was
  given the host name through a variable taken from the probe's output, for
  the same reason.
- **Readings between the steps** were Claude's own one-line commands, each
  read-only. They are not in the document.
- **One change of order at the end.** The test machine's service was stopped
  with the first line of "Uninstalling", FIOS-STB-01's service was started
  again, and then the whole of "Uninstalling" was run on the test machine, as
  printed. That shortened the time the household had no printer. Nothing in
  "Uninstalling" needs the other proxy to be stopped.

## The machine before the walk

At 19:36:27Z, read with one command: no service registered, no registry key
for one, no program folder, no data folder, and no firewall rule naming a
SecretPrinter program. That is how the walk of 2026-10-06 left it.

## The download

Edwin opened the release page for `v0.1.0-rc.3` in Microsoft Edge and clicked
the two files under Assets. Edge put them in the user's Downloads folder and
showed no warning, by his account.

- `SecretPrinter-0.1.0-rc.3-win-x64.zip`, 37,552,345 bytes, written 19:39:33Z;
- `SecretPrinter-0.1.0-rc.3-win-x64.zip.sha256`, 104 bytes, written 19:39:36Z.

**Both carry Windows' mark for a file from the internet.** Each has an
alternate data stream named `Zone.Identifier` holding `ZoneId=3`, with
`ReferrerUrl` on `github.com` and `HostUrl` on
`release-assets.githubusercontent.com`. The zip has a second stream as well,
named `SmartScreen`, seven bytes long, holding the word `Anaheim`. What Windows
does with that stream is not established.

## The two checks

**Check 1,** as printed: `Get-FileHash` gave
`F8310DD6B5B061925958C1A25521C32E4C9979DC2BAE751E0C69ABCBC827C315`, and the
`.sha256` file holds the same 64 characters, in capitals, two spaces, and the
zip's name. That is the value the signing job recorded and the digest GitHub
shows for the published file.

**Unpacking.** `Expand-Archive -LiteralPath <zip> -DestinationPath <walk folder>`
gave 216 files. **None of them carries the internet mark:** `Expand-Archive`,
in Windows PowerShell, did not pass it on. Nothing asked about the mark at
any later step. Unpacking with File Explorer was not tried.

**Check 2,** as printed, with the folder filled in, at about 19:53Z:

| Count | Ours | Status | Signer |
| --- | --- | --- | --- |
| 185 | False | `Valid` | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | False | `Valid` | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | False | `Valid` | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 12 | True | `Valid` | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |

The same four counts as the trial build of 2026-10-04. At 19:56:19Z the check
was run again with a column for the timestamp: every signature carries one.

**What the build is,** read from the folder at the same time:

- the stamp in `SecretPrinter.Service.dll`:
  `0.1.0-rc.3+c5cc8c4ba5f58b438a42b3a8aefc382f9487056b`, the version and the
  commit the tag names;
- the runtime in `SecretPrinter.Service.runtimeconfig.json`:
  `Microsoft.NETCore.App 10.0.12`, the same as the trial build of 2026-10-04.

## The firewall rules

The two `New-NetFirewallRule` commands, as printed but for the program's path,
were pasted as one block at about 19:59Z, before `C:\Program Files\SecretPrinter`
existed. Windows accepted both. Each printed `Enabled : True`,
`Profile : Private`, `Direction : Inbound`, `Action : Allow` and
`PrimaryStatus : OK`. The document's reading command then showed both rules
enabled, `Private`, naming that path, on 5353 and 631.

`Get-NetConnectionProfile` listed only `Ethernet`, `Private`, because the
printer-side `Wi-Fi` was not connected yet; it does not join that network by
itself. Read again after Edwin connected it, at about 20:07Z, both read
`Private`.

## The example and its refusal

In the walk folder, as printed, at 20:03:57Z by the program's own clock:

- `[guid]::NewGuid()` printed a UUID.
- `.\SecretPrinter.Service.exe --print-example-config > secretprinter.json`
  wrote the example. It shows `"printerCertificateSha256": ""` and
  `"uuid": ""`, as it has since 2026-10-07, and names `Ethernet 2` as the
  client interface and `Wi-Fi` as the printer's.
- `.\SecretPrinter.Service.exe --config secretprinter.json`, run on the example
  before anything was filled in, logged `Configuration is not usable, so the
  service will not start.` and three problems, in this order:
  `printerCertificateSha256: required, and deliberately has no default`, with
  where to measure it; `advertise.uuid: required, and deliberately has no
  default`, with `Generate one with [guid]::NewGuid() in PowerShell`; and
  `clientInterfaces: No interface is named 'Ethernet 2'.` The test machine has
  no adapter of that name. Exit code 3.

That is the first run on Windows of the example as it is now printed, and the
first run of the published program. Both did what the finding of 2026-10-07
says.

The last problem lists every interface Windows reports. On the test machine
that is 44 names, most of them hidden ones (`Local Area Connection* 1` to `10`,
filter drivers such as `Ethernet-WFP Native MAC Layer LightWeight
Filter-0000`, `Teredo Tunneling Pseudo-Interface`, `6to4 Adapter`). The two the
configuration needed are among them. The document's own command for listing
adapters, `Get-NetAdapter`, showed four that were up.

## The probe and the certificate

With `Wi-Fi` connected by hand, the document's two commands under Configuring
showed `Ethernet` at 192.168.1.207 and `Wi-Fi` at 192.168.12.142, both `Private`.

**The probe** from the release folder, as printed with `--interface
192.168.12.142`, at about 20:11Z: exit code 0. Two replies, both from the
printer at 192.168.12.180, one to each of two rounds of questions. Five service
types, each with one instance named `EPSON ET-3760 Series`; the `._ipp._tcp.local`
name appears under `_ipp._tcp.local` and again under
`_universal._sub._ipp._tcp.local`, as the document says. Its `SRV` lines give
host `EPSON000000.local` and port 631 for the two IPP names. Under each instance
there are four `ADDRESS` lines, 20 in all, one IPv4 and three IPv6, as the
document says. This is the first run of the signed probe of this build against
the printer, and the first since the probe was given `REQ-SEC-016` on
2026-10-06.

**The certificate command,** as printed, with 192.168.12.180 and the host name:

- `SHA256 3834787341192E3B7B74FFE51A9EE0E67E2DEBD6B4A060C6B203CB5252BE80EF`
- `SHA1   201B4A53AF65255258D0FE5AC8115E2073A16675`
- `O=SEIKO EPSON CORP., CN=EPSON000000`
- `Tls12`

The same as on 2026-09-15 and 2026-10-06.

## The configuration

Edwin edited it by hand, at about 20:19Z; the step gave
`notepad .\secretprinter.json` to open it. Three values changed: `clientInterfaces`
to `[ "Ethernet" ]`, the fingerprint above, and the new UUID. The others were
already right for this machine and printer, and were left as the example has
them. As saved the file had 20 lines, the example's two blank lines included,
was 1,210 bytes, began with the bytes 255 and 254 (UTF-16, as PowerShell's `>`
wrote it), and read as JSON. On 2026-10-06, on the same machine, the saved file
had lost the two blank lines; this time it had not. What removed them then is
still not established.

## The install

All in an elevated window, as printed, with `<release-folder>` and `<your>`
both the walk folder:

- **The data folder and the grant.** `New-Item` made `C:\ProgramData\SecretPrinter`
  at 4:22 pm Eastern by its own listing, and `icacls` reported one file
  processed and none failed.
- **The two `Copy-Item` lines.** Silent. Then the program folder held **217
  files: the 216 of the release and the configuration**, which had been made in
  the release folder as Configuring has the reader do. Its stamp was the one
  above. The copy in the data folder was 1,210 bytes, and `icacls` showed
  `NT AUTHORITY\LOCAL SERVICE:(I)(M)` on it, inherited from the folder's
  `(OI)(CI)(M)`.
- **The `Remove-Item` line, run for the first time.** Silent. The program
  folder then held 216 files and no configuration, and the copy in the data
  folder was still there, 1,210 bytes, with the same permission. Until now this
  stray copy had been worked out by reading the steps; it is now seen.
- **`sc.exe --% create`,** as printed, at about 20:27Z: `[SC] CreateService
  SUCCESS`. `sc.exe qc` showed `AUTO_START`, the program path in quotes with
  its three arguments exactly as the document gives them, and
  `SERVICE_START_NAME : NT AUTHORITY\LocalService`. The System log recorded
  event 7045, "A service was installed in the system", at 20:27:55Z.

## FIOS-STB-01 stopped

Read first, at 20:29:32Z: running, listening on 631 at 192.168.1.161 and at its
link-local address, and its log ending with a start at 19:12:54Z that found the
printer, probed clear and announced. Edwin said that he had since changed its
printer-side adapter back to one it had used before, because the one recorded
on 2026-10-06 "disconnected constantly". The log names `Ethernet` with index 18
and `Wi-Fi` with 27, where the record of 2026-10-06 has 15 and 32. What changed
the `Ethernet` index, and why the service had started at 19:12:54Z, were not
asked. *(Status note, 2026-10-09, by Claude, Claude Opus 5.5: FIOS-STB-01's log,
read on 2026-10-09, shows a stop and a start within a second at 19:12:32Z and
again at 19:12:53Z, and Windows had last started at 00:29:01Z that day. Edwin
West said he had restarted the service. What changed the `Ethernet` index is
still not established
([finding](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md)).)*

Stopped at **20:31:35Z**. Its log: `Service 'SecretPrinter' stopping.`, `No
longer accepting print jobs on Ethernet.`, `Advertisement retracted (goodbye
records sent).`, the two counting lines, and `SecretPrinter stopped.` From here
the household had no printer on the client network.

## The failure provoked on purpose

**How.** In a second, unelevated PowerShell window on the test machine, a
`TcpListener` was started on 192.168.1.207, port 631, the client-side address
and the port the configuration names. It printed `holding 631`. No firewall
prompt was reported. Then, in the elevated window, `sc.exe start SecretPrinter`,
a wait of 20 seconds, and a reading.

**What `sc.exe start` printed,** at 20:52:29Z: `STATE : 2 START_PENDING`, with
a process id, `WIN32_EXIT_CODE : 0` and `WAIT_HINT : 0x7d0`. The same as for a
good start.

**What the log says,** in order (link-local addresses and the printer's host
name replaced):

- 20:52:29Z: `Service 'SecretPrinter' starting under the service control
  manager.`, `SecretPrinter starting.`, the configuration's settings, both
  sockets, the printer found at 192.168.12.180, `Probing for the advertised
  names`;
- 20:52:30Z: `Probe clear`;
- 20:52:32Z: `Announced. Answering queries.`, then
  - ERROR `Could not listen on every address published for Ethernet
    (192.168.1.207, (link-local address)): Only one usage of each socket
    address (protocol/network address/port) is normally permitted. The
    addresses are read once, at startup; if one has changed, restart the
    service to publish and listen on the current ones. Stopping, so that no
    address is published with nothing listening behind it.`
  - ERROR `A part of the service failed, so the whole service is stopping:
    SocketException: Only one usage of each socket address ...`
  - `Advertisement retracted (goodbye records sent).`
  - `Served 0 quer(ies) of 0 seen; ...` and `By transport: ...`
  - `SecretPrinter stopped.`
  - ERROR `Service stopped because of an error: Only one usage of each socket
    address ...`
  - ERROR `Ending the process with exit code 4, so that Windows does not go on
    showing a service that has failed as running. Start the service again once
    the cause is put right.`

The four error lines are those the finding of 2026-10-07 predicted, in its
order. The service offered the printer for less than a second: the
announcement and the goodbye are logged in the same second.

**What Windows showed:**

- `sc.exe query SecretPrinter` at 20:52:49Z: `STATE : 1 STOPPED`,
  **`WIN32_EXIT_CODE : 1067 (0x42b)`**, `SERVICE_EXIT_CODE : 0 (0x0)`. Windows
  error 1067 is `ERROR_PROCESS_ABORTED`, "The process terminated unexpectedly"
  ([System Error Codes (1000-1299)](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1000-1299-),
  read 2026-10-08). The 4 that the log names is not what Windows shows.
- The System log, from the Service Control Manager, event **7034** at
  20:52:32Z: "The SecretPrinter service terminated unexpectedly. It has done
  this 1 time(s)."
- The Application log: no entry naming SecretPrinter in the half hour that
  covered this start.

So the change of 2026-10-07 does what it was made for. The failed part stopped
the whole service, the goodbye went out, and Windows does not go on showing a
failed service as running. What Windows calls it is an unexpected end, error
1067, not an exit code of 4.

**The port was then released** in the second window: `released`.

## The good start

At about 21:03Z, as printed: `sc.exe start` printed `START_PENDING` again; the
`sc.exe query` straight after it read `STATE : 4 RUNNING`, which is what the
document's comment says to expect; and 20 seconds later still `RUNNING` with
both exit codes 0. The log from 21:03:14Z is the same as above up to
`Announced. Answering queries.` at 21:03:17Z, and then `Accepting print jobs
on 192.168.1.207:631 (Ethernet); permitted clients: 192.168.1.0/24.` and the
same on its link-local address. **From 21:03:17Z the household had a printer
again,** through the test machine.

## The print

From the iPhone. "yes it printed." The time of the tap was not noted. Edwin
sent three screenshots:

- **The list,** at 5:09 pm by the phone: under **Known Printers**, `EPSON
  ET-3760 Series`; under **Other Printers**, `SecretPrinter (ET-3760)`, with the
  note "SecretPrinter proxy. Capabilities read from the printer at 2026-10-08
  21:03:14Z", which is the time the service asked the printer at its start.
- **The options screen,** after picking the proxy: the printer shown as `EPSON
  ET-3760 Series`.
- **The information screen** that the button beside the proxy's row opened, by
  Edwin's account: Name and Model `EPSON ET-3760 Series`, with the printer's ink
  levels.

**The log,** read at 21:11:45Z: ten connections accepted since the start, all
from the iPhone's link-local address. Each was relayed to 192.168.12.180:631
with "encryption to printer: TLS 1.2, certificate matched the pinned
fingerprint", and each has its `completed` line. None failed and none was
refused. The first opened at 21:07:49Z and the last ended at 21:10:39Z. The
largest opened at 21:08:15Z and carried 310,151 bytes to the printer in 79
seconds.

`operating.md` says the proxy is listed "in its list of known printers". Here
it was under **Other Printers**. On 2026-09-21, 2026-09-22, 2026-09-25 and
2026-09-29 the findings record both rows under "Known Printers". This was the
first time the iPhone met an installation whose UUID was generated that day.
Why the list put it where it did is not established. The rest of that section
held: two rows; picking the proxy turned the selection into the printer's own
name; and the page went through the proxy.

The information screen for the proxy's row naming the printer fits iOS taking
the name from what the printer says about itself, which the proxy passes on,
in its advertisement and in the print connection. That is not established.

## Stopped, FIOS-STB-01 back, and the uninstall

- **The test machine's service stopped** with `sc.exe stop`, the first line of
  "Uninstalling", at 21:40:21Z: `STOP_PENDING`, then `STOPPED` with exit code 0.
  Its log at 21:40:22Z: the same six lines as FIOS-STB-01's stop above, with
  `Served 78 quer(ies) of 2071 seen` and `IPv4 answered 51 of 1920 seen; IPv6
  answered 27 of 151 seen`. From here the household had no printer again.
- **FIOS-STB-01's service started** at 21:43:16Z, after Edwin had checked on
  his own that it reached the printer through `Wi-Fi`. It found the printer,
  probed clear at 21:43:18Z, and was accepting print jobs at **21:43:20Z**.
- **The test machine's log was copied out** before the uninstall removed it:
  27,741 bytes, 230 lines, the same SHA-256 as the original. It holds
  link-local addresses, the printer's host name and UUIDs, and is not in this
  repository.
- **"Uninstalling", as printed, every command in order,** at about 21:48Z:
  `sc.exe stop` printed `[SC] ControlService FAILED 1062:` and `The service has
  not been started.`, as the document says it does for a stopped service;
  `sc.exe delete` printed `[SC] DeleteService SUCCESS`; the two
  `Remove-NetFirewallRule` lines and the three `Remove-Item` lines printed
  nothing.
- **Read at 21:48:52Z:** no service, no registry key, no program folder, no
  data folder, and no firewall rule naming a SecretPrinter program.

## How long the household had no printer

The printer was not offered on the client network from 20:31:35Z to 21:03:17Z,
31 minutes 42 seconds, but for the moment at 20:52:32Z, and from 21:40:22Z to
21:43:20Z, 2 minutes 58 seconds: **34 minutes 40 seconds** in all. Before the
walk this could not be sized; the walk of 2026-10-06, which covered less, cost
53 minutes.

## Where the document and the machine disagreed

Each is corrected in `operating.md` in the commit that carries this finding.

1. **The firewall commands named a placeholder, and which file to name came
   after them.** Both commands named `C:\path\to\SecretPrinter.Service.exe`.
   The paragraph saying the rules "must name the file the service actually
   runs" comes after both. A reader following the document in order meets the
   placeholder first. Both commands now name
   `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`, which is where
   the install step puts the program, and a sentence before them says so and
   that the file need not exist yet. The second command's block also lacked the
   `# Run as Administrator.` line the first has; it has it now.
2. **Unpacking had no command.** "Unpack the zip into a new folder" is all the
   document said. It now gives the `Expand-Archive` command that was run, and
   says what the browser's mark did.
3. **The iPhone's list.** "In its list of known printers" is replaced by what
   was seen, with the earlier occasions.
4. **What Windows shows when the service fails after it has started.** The
   document listed it as not measured: what `sc.exe start` prints, what
   `sc.exe query` shows, which event is recorded. Each is now given. That a
   recovery action set with `sc.exe failure` would run is still not measured.
5. **The exit code under `--service`.** Under Running the document said that
   under `--service` "the process ends with the same code". That is what the
   code does and what the log says, but it is not what Windows shows, which is
   error 1067. The sentence now says both.

Also now said in `operating.md`, where it had been silent or out of date: that
`sc.exe start` prints `START_PENDING` and that this is not a failure; what the
program printed when run on the example; that the `Remove-Item` line has been
run, with what it removed; and this walk among the results under "Where the
program comes from", "Checking a release before you install it" and
"Uninstalling".

Corrected elsewhere, because the walk made them out of date:

- **`README.md`:** the entry under "Known problems" headed "What Windows shows
  when the service fails after it has started has never been measured",
  rewritten; notes on `REQ-LIF-004` and `REQ-ADV-021`, which said the change
  had not been run under Windows or seen on a machine; a note on the
  `System.Diagnostics.EventLog` dependency, which said that whether
  `ServiceBase` writes an Application log entry under `LocalService` had not
  been measured; and open question 10, which said SecretPrinter had been run on
  Windows 11 five times for under half an hour in all.
- **`CHANGELOG.md`:** a note under `0.1.0-rc.3`, whose "What happened" says
  nobody had downloaded and installed it.
- **`docs/verification.md`:** this finding added to the evidence for
  `REQ-DIST-005`, the two checks, which until now had been run only on a trial
  build downloaded from a workflow run.
- **Status notes** on the four findings of 2026-10-07 that said what this walk
  would show.

## Seen, and not corrected

None of these is a fault in what the program does, and none is needed for
0.1.0. The first two would change a message that a release has been measured
with.

- **The refusal for an interface that does not exist lists 44 names** on this
  machine, most of them hidden. The adapters that matter are among them.
- **The listener's error line advises a restart "if one has changed".** That
  fits the case `REQ-ADV-021` is written for, an address that has gone. Here
  the cause was another program holding the port, which Windows' own message
  in the same line names. `operating.md` now says what that message means.
- **The SmartScreen stream** on the downloaded zip: what Windows does with it
  is not established. Nothing in the walk asked about it or about the internet
  mark.
- **1,920 queries over IPv4 in 37 minutes** on the test machine, against 151
  over IPv6. The counts have varied widely before (1,418 in one run on 2026-10-06
  and 4 in the next) and are not explained.
- **A connection at 21:30:07Z,** twenty minutes after the print, carried 4,732
  bytes to the printer. The walk asked for no print then. What the iPhone
  asked for is not established.

## What Claude predicted wrongly, and a mistake

- **The probe's third round.** Claude predicted three rounds of questions. The
  probe sends the third only when it still lacks an address for a host it was
  told of, and the printer had sent the address with its second answer. Claude
  had not read that condition.
- **The lines of FIOS-STB-01's stop.** Claude predicted a `Stopping.` line and
  no `No longer accepting print jobs` line. `Stopping.` is logged only when the
  wait on the service's parts ends in a cancellation; on an ordinary stop the
  parts simply end. Claude had read the line and not followed when it is
  reached. The prediction for the test machine's own stop, made after reading
  that, held.
- **A filter left out the line that mattered.** The reading after the provoked
  failure dropped advertisement record lines by matching the word `publish`
  between spaces. The first error line contains "restart the service to publish
  and listen", so it was dropped too, and the paste showed three of the four
  error lines. The log was read again unfiltered from `Announced`, and the line
  was there. The filter was then anchored to the level, `INFORMATION`, that
  record lines carry. This is the same kind of mistake as the filter of
  2026-10-06 that left out `Waiting`.

Two readings held more than was predicted: a second stream on the downloaded
zip, and `169.254.` addresses on three adapters besides the two VMware ones.
Every other prediction made before a command held.

## What this does not show

- **That a recovery action would run.** No `sc.exe failure` action was set.
  Windows recorded the end as unexpected; whether a recovery action would then
  run is not measured.
- **The cause `REQ-ADV-021` names.** The listener was made to fail by a port in
  use, not by a published address that had gone. By the code, any socket
  error while opening the listeners takes the same path; that cause was not
  tried.
- **The account of the running process.** `sc.exe qc` shows the service
  registered under `NT AUTHORITY\LocalService`. The account of the process
  itself was not read.
- **The process's own exit code.** The log says 4. Windows shows 1067. Nothing
  read the process's exit code directly.
- **Unpacking with File Explorer,** and running the programs from File Explorer,
  where the internet mark could matter.
- **An update from one release to the next,** followed from the document. Edwin
  chose to uninstall at the end of the walk, so no install was left to update.
- **A start at boot** of this install. It was registered `start= auto` and
  uninstalled without a restart of Windows; a start at boot was measured on
  2026-10-06.
- **Another device.** One iPhone, which had printed to the printer directly
  before.
- **Release 0.1.0.** It will be built later, from the same source code and
  changed documents. Its build will carry whatever runtime the SDK on GitHub's
  build machine supplies then, and its stamp will name its own commit. *(Status note, 2026-10-09, by Claude, Claude Opus 5.5: built and
  published on 2026-10-08, carrying .NET 10.0.12
  ([finding](2026-10-08-the-first-release-was-published.md)), and run on
  2026-10-09 on FIOS-STB-01, installed by the update steps
  ([finding](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md)).)*

## Where it was measured

- **The test machine:** the machine of the findings of 2026-10-04 and
  2026-10-06. Its Windows version and that it has no .NET were recorded then and
  not read again. Ethernet on the client-side network, `Wi-Fi` on the
  printer-side network, both `Private`.
- **FIOS-STB-01,** stopped and started again, and read before each.
- **The iPhone,** by Edwin's account and his three screenshots, which show a
  personal document and are not in this repository.

## Left on the test machine

The zip and its `.sha256` in the Downloads folder; the walk folder, holding the
unpacked release and the configuration; and the copy of the service's log. All
are Edwin's to keep or delete.
