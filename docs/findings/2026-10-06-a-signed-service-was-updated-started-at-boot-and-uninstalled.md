# A signed service was updated, started at boot and uninstalled

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-06. Reviewed by a human before merge.*

**Status: measured. On 2026-10-06, on the test machine, the unsigned program
folder installed on 2026-10-04 was replaced by the folder the release workflow
signed for the trial tag `v0.1.0-rc.2`. The service started from it under its
existing registration, with a configuration made by hand, and an iPhone printed
a page through it. The service was then set to start automatically, Windows was
restarted, and the service started at boot, waited for its printer side, and
offered the printer when that side was connected. Last, SecretPrinter was
uninstalled by the steps in [`operating.md`](../operating.md), and what those
steps left behind was read. The walk found that `operating.md` had no update
steps for a release, that its uninstall steps left the program folder and the
data folder in place, and that the probe prints four `ADDRESS` lines where the
document says one. It also found that an earlier finding names the wrong commit
for the folder it tested. All are corrected in the commit that carries this
finding. No release exists.**

No IPv6 address, MAC address or device name appears in this finding. The second
machine is called the test machine, as in the two findings it follows. The
printer's host name is written `EPSON000000`. The folder the work was done in is
written `<walk folder>`, and the UUID in the configuration is not given.

**About the times.** Times ending in `Z` are UTC and come from a machine's own
clock or a service's log. Where a step's time is given "by the conversation's
clock", the machine did not record it, and the time is that of the message in
which Edwin sent the result, to the minute. All of it falls on 2026-10-06 in
UTC and in US Eastern time, where it ran from about 12:55 pm to 2:40 pm.

## Why this was done

- [The finding of 2026-10-04](2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)
  and
  [the earlier finding of 2026-10-06](2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)
  both list starting at boot, updating an installed release and uninstalling as
  not exercised, and the second lists installing the signed files as a service.
- `operating.md`, under "Updating an installed service", said its procedure was
  for an installation built from source. Its steps were `git pull` and
  `dotnet publish`. Someone with a release had nothing to follow.

Before the walk began, what would complete it was stated: the signed folder is
in `C:\Program Files\SecretPrinter` in place of the unsigned one, as a clean
folder; the service starts under `LocalService` with a configuration made by
hand; an iPhone prints through it; the two firewall rules name the installed
program; and `operating.md` has update steps for a release that were followed
as written. The last of these was met only in part; see the next section.

## What "following the instructions" means here

Edwin ran every command. Claude gave them one at a time and said what it
expected before each.

- **The update steps did not exist in the document when they were run.** Claude
  set them out in plain words in the conversation, Edwin ran them, and they were
  written into `operating.md` afterwards, in the commit that carries this
  finding. So the document's update steps have not been followed from the
  document by anyone.
- **The uninstall began from the document.** The two `sc.exe` commands and the
  three commands under "Uninstalling" were the document's. The two that remove
  the folders were run first and written into the document afterwards.

Where a command differed from the text now in the document:

| Step | Difference |
| --- | --- |
| Checking and unpacking | The two checks and `Expand-Archive` were run as one line. The zip was the one checked on this machine in the earlier walk; it was unpacked again into a new folder, because the first unpacked folder by then held a configuration file. |
| Stopping and removing | `Stop-Service`, `Remove-Item` and a `Test-Path` on one line. The service was already stopped. |
| Copying and confirming | `Copy-Item`, then a file count, a `Test-Path` on the program, the version, and the signature check, on one line. The document does not ask for the count or the `Test-Path`. |
| Starting | `Start-Service`, a 15-second wait, and the log read through a filter that left out the record listing and replaced link-local addresses. |
| The firewall rules, on uninstall | Named `SecretPrinter mDNS (release test)` and `SecretPrinter IPP (release test)` on this machine since 2026-10-04. The document's names have no suffix. |
| The log, on uninstall | Copied to `<walk folder>` before the document's command removed it. |

## The machine before the walk

Read at 16:55:35Z:

- **The service:** `START_TYPE : 3 DEMAND_START`, the program path
  `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe` in quotes followed
  by `--config`, `--log-file` and `--service`, and
  `SERVICE_START_NAME : NT AUTHORITY\LocalService`. Its state was `Stopped`.
- **The program folder:** 216 files. `SecretPrinter.Service.exe` read
  `NotSigned`.
- **The two firewall rules:** both enabled and `Private`, on 5353 and 631, and
  both naming the program in `<walk folder>`, where the earlier walk had pointed
  them.

### The installed folder was not built at `26ddfbe`

The version stamped in `SecretPrinter.Service.dll` read
`1.0.0+0ae262d6308e556abb96d8095563da1880e4e010`. Claude had predicted
`26ddfbe`, because
[the finding of 2026-10-04](2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)
said the folder was made "from a clean working tree at `26ddfbe`".

The part after the `+` is the commit the building clone was on when the file was
compiled. A second reading listed SecretPrinter's own twelve program files:

- **All twelve carry `0ae262d`,** the two `.exe` files included.
- **All twelve were written between 17:06:00Z and 17:06:12Z on 2026-10-04.** Git
  records `26ddfbe` as committed at 17:10:31Z that day.

So the folder was compiled before `26ddfbe` existed, with the clone at
`0ae262d`. `publish-release.ps1`, which made the folder, is new in `26ddfbe`, so
it was in the working folder and not yet committed. The tree was not clean.

- **The compiled source is the same at both commits.** `26ddfbe` changed three
  files from `0ae262d`: `README.md`, `docs/verification.md` and the new
  `publish-release.ps1`. None is compiled. What the folder was shown to do is
  not affected.
- **Not shown:** whether the script that ran was byte for byte the one committed
  four minutes later.
- **The mistake is Claude's.** The finding named the commit that carried the
  script. The stamp, which shows the commit the files were built from, was not
  read when the finding was written.

The statement was in three places: twice in that finding, and in the
`REQ-DIST-011` entry of [`verification.md`](../verification.md). All three are
corrected.

## Readings on the signed folder

Read at 17:00:16Z in the unpacked folder of the earlier walk. The first three
fill blanks
[that walk's finding](2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)
left.

- **All 202 program files read `Valid`,** in the same four groups: 185, 3 and 2
  signed by Microsoft, and 12 signed
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. The signing
  certificate had expired at 03:27:46Z, 13 hours 32 minutes 30 seconds earlier.
- **Every one of the 202 carries a timestamp,** Microsoft's files too. The
  earlier walk did not read that.
- **The signed build carries .NET runtime 10.0.12:**
  `SecretPrinter.Service.runtimeconfig.json` lists
  `Microsoft.NETCore.App 10.0.12`. This is the first reading of the runtime a
  build from the release workflow carries. The development machine's folder of
  2026-10-04 carried 10.0.11.
- **The version stamp is
  `0.1.0-rc.2+3bcdd4c7784aef0355a1d2d93860039e93f69278`:** the version in
  `Directory.Build.props` at that tag, and the commit the tag `v0.1.0-rc.2`
  names. So the stamp identifies which release a file came from.
- **The folder held 217 files:** the 216 of the zip and the configuration made
  by hand.

## The update

1. **Check and unpack,** at 17:06Z by the conversation's clock. `Get-FileHash`
   and the `.sha256` file showed the same 64 characters, the value recorded on
   2026-10-04. The zip unpacked into a new folder as 216 files, and the
   signature check showed the four groups above, all `Valid`.
2. **Stop and remove,** at 17:09Z by the conversation's clock.
   `Stop-Service SecretPrinter` and
   `Remove-Item "C:\Program Files\SecretPrinter" -Recurse` printed nothing.
   `Test-Path` on the folder then read `False`, and the service `Stopped`.
3. **Copy,** at 17:11Z by the conversation's clock.
   `Copy-Item <new folder> "C:\Program Files\SecretPrinter" -Recurse` printed
   nothing.
4. **Confirm, before starting.** 216 files; the program present at the path the
   service is registered with; the version stamp
   `0.1.0-rc.2+3bcdd4c7784aef0355a1d2d93860039e93f69278`; and the four signature
   groups, all `Valid`.
5. **Start and read the log.** See "The run as an installed service" below.

Nothing in these steps touched the service's registration, the log or the data
folder.

## Two steps that are not part of an update

Both were made on this machine because of how the two earlier walks left it.

- **The configuration.** `C:\ProgramData\SecretPrinter\secretprinter.json` was
  the file copied from FIOS-STB-01 on 2026-10-04: 1,210 bytes, written
  2026-10-04 18:35:26Z. Edwin chose to run the installed service with the
  configuration made by hand in the earlier walk. The document's `Copy-Item`
  put it there, at 17:07Z by the conversation's clock: 1,206 bytes, its first
  two bytes 255 and 254, and its SHA-256 equal to the hand-made file's.
- **The firewall rules.** `Set-NetFirewallRule` pointed both at
  `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`, and both read back
  enabled, `Private`, on 5353 and 631. An update leaves the rules alone, because
  the path does not change.

## The run as an installed service

FIOS-STB-01's service was stopped at 17:13:55Z, so that one machine offered the
printer.

- **17:15:25Z** `Start-Service`. Fifteen seconds later the service was
  `Running`. **The configuration was accepted:** the start-up block shows
  `Ethernet` at 192.168.1.207, the two instance names, the pin, the UUID, and
  `advertised as : SecretPrinter (ET-3760) on secretprinter.local:631`, each as
  the file has it. So a service registered under `LocalService` read a
  configuration written as UTF-16.
- **17:15:25Z** a warning: `Waiting for the printer-side interface: Interface
  'Wi-Fi' holds 192.168.12.142 but is not up.` Edwin had not connected the test
  machine to the printer's network. Nothing was offered.
- **17:20:27Z**, after Edwin connected it: `The printer-side interface 'Wi-Fi'
  is usable. No longer waiting for it.` The wait was 5 minutes 2 seconds. The
  printer answered in the same second. `Probing`, then **17:20:28Z** `Probe
  clear`, **17:20:30Z** `Announced. Answering queries.`, and two listeners: on
  192.168.1.207:631 and on the client interface's link-local address, port 631.
  Nothing restarted the service.
- **17:23:45Z to 17:24:52Z** eight connections, each from a link-local client
  address and each relayed with `encryption to printer: TLS 1.2, certificate
  matched the pinned fingerprint`.
- **The page printed.** Edwin noted tapping Print at 1:24 pm Eastern, which is
  17:24Z, and that the page started two seconds later and came out.
- **17:28:18Z** `Stop-Service`: `Service 'SecretPrinter' stopping.`, `No longer
  accepting print jobs on Ethernet.`, `Advertisement retracted (goodbye records
  sent).`, `Served 46 quer(ies) of 1482 seen; 1436 were for other services and
  were ignored.`, `By transport: IPv4 answered 34 of 1418 seen; IPv6 answered
  12 of 64 seen.`, `SecretPrinter stopped.`

The eight connections:

| Accepted | Count | Completed | Bytes to the printer, and back |
| --- | --- | --- | --- |
| 17:23:45Z | 3 | 17:23:47Z | 662 and 498 on each |
| 17:24:37Z | 1 | no ending line | not logged |
| 17:24:38Z | 2 | 17:25:17Z | 3,436 and 4,138 on each |
| 17:24:47Z | 1 | 17:26:05Z | 302,956 and 10,071, in 77.6 s |
| 17:24:52Z | 1 | 17:25:23Z | 1,235 and 616 |

The registration named `LocalService` when it was read at 16:55:35Z and was not
changed before this run. The account of the running process was not read.

## The start at boot

FIOS-STB-01's service stayed stopped.

- `sc.exe config SecretPrinter start= auto` printed `[SC] ChangeServiceConfig
  SUCCESS`, and `sc.exe qc` then showed `START_TYPE : 2 AUTO_START`.
- Edwin restarted Windows with **Restart**, signed in, and did not connect the
  printer's network.
- **Windows gives its boot time as 17:34:51Z.** **The service's
  `SecretPrinter starting.` line is at 17:34:58Z,** seven seconds later.
- **The client side was ready:** the log shows `Ethernet -> 192.168.1.207
  (index 4)`.
- **17:34:58Z** a warning: `Waiting for the printer-side interface: Interface
  'Wi-Fi' has no IPv4 address, so it cannot be used.` **17:35:08Z** the reason
  changed and was logged again: the interface held an address beginning
  `169.254.` and was not up. At 17:36:17Z the service was `Running`, `Wi-Fi`
  read `Disconnected` and `Ethernet` read `Up`.
- Edwin connected the printer's network by hand. The time was not noted.
- **17:39:14Z** `The printer-side interface 'Wi-Fi' is usable. No longer
  waiting for it.` The wait was 4 minutes 16 seconds. The printer answered in
  the same second. **17:39:15Z** `Probe clear`. **17:39:17Z** `Announced.
  Answering queries.`, and the same two listeners. The log's `SecretPrinter
  starting.` line was still the one of 17:34:58Z: nothing had restarted the
  service.
- **17:39:18Z** a connection from 192.168.1.152 was accepted and relayed with
  the pinned certificate matched. No print had been asked for. Which device
  that was is not established.
- `sc.exe config SecretPrinter start= demand` and `Stop-Service` followed. The
  start type read `3 DEMAND_START`. The log ends at **17:41:48Z** with a clean
  stop: `Served 2 quer(ies) of 8 seen`, `IPv4 answered 1 of 4 seen; IPv6
  answered 1 of 4 seen`. The connection from 192.168.1.152 has no ending line.

This is the first measurement of SecretPrinter starting at boot on any machine.
[The finding of 2026-09-22](2026-09-22-the-withdrawn-start-on-hardware.md)
listed it as the case the waiting was built for, and not run.

## The uninstall

FIOS-STB-01's service was running again by then, and the test machine's service
was stopped throughout.

1. **The registration,** at 18:20Z by the conversation's clock, by the two
   commands under "As a Windows service". `sc.exe stop SecretPrinter` printed
   `[SC] ControlService FAILED 1062:` and `The service has not been started.`,
   because the service was already stopped. `sc.exe delete SecretPrinter`
   printed `[SC] DeleteService SUCCESS`. `Get-Service` then found no service of
   that name.
2. **The firewall rules and the log,** at 18:36Z by the conversation's clock, by
   the three commands under "Uninstalling". They printed nothing. Afterwards no
   firewall rule had a name beginning `SecretPrinter`, and the log was gone. A
   copy of the log, 31,527 bytes, had been put in `<walk folder>` first.
3. **What was left.** By then every command the document gave had been run.
   Read at 18:37Z by the conversation's clock:

   | Looked for | Found |
   | --- | --- |
   | A service named `SecretPrinter` | none |
   | The registry key `HKLM:\SYSTEM\CurrentControlSet\Services\SecretPrinter` | none |
   | `C:\Program Files\SecretPrinter` | present, 216 files |
   | `C:\ProgramData\SecretPrinter` | present, holding `secretprinter.json`, 1,206 bytes |
   | The permission on that folder | `NT AUTHORITY\LOCAL SERVICE:(OI)(CI)(M)`, as the install step granted it |
   | A firewall rule, under any name, whose program path contains `SecretPrinter` | none |

   The last row answers a question that had not been asked before. `operating.md`
   says Windows Firewall "will usually prompt on first run", and SecretPrinter
   had been run from a console window on this machine on 2026-10-04 and
   2026-10-06. Windows had added no rule of its own.
4. **The two folders,** at 18:38Z by the conversation's clock.
   `Remove-Item "C:\Program Files\SecretPrinter" -Recurse` and
   `Remove-Item "C:\ProgramData\SecretPrinter" -Recurse` printed nothing, and
   `Test-Path` then read `False` for both.

## FIOS-STB-01 did not hear the printer when its service was started again

FIOS-STB-01 runs the build of `64e8fc2`. Its service had been stopped at
17:13:55Z.

- **17:43:21Z** `Start-Service`. The service was `Running`. **17:43:27Z** a
  warning: `The printer did not answer at startup: 'EPSON ET-3760 Series._ipp._tcp.local'
  did not answer within 5s on Wi-Fi (192.168.12.163, IPv4 index 32). Nothing
  wrong was found locally: adapter 'Wi-Fi' is up, holds 192.168.12.163, and the
  address is preferred.` The service went on asking on its schedule and offered
  nothing.
- **Between then and 17:45:58Z** (no clock was taken) Edwin ran two commands.
  `Get-NetAdapter` gave `Wi-Fi` as `Up` at 72.2 Mbps.
  `Test-NetConnection 192.168.12.180 -Port 631` reported
  `InterfaceAlias : Ethernet`, `SourceAddress : 192.168.1.161`, the ping timed
  out, and `TcpTestSucceeded : False`.
- **17:45:58Z** the log's last line was still the warning.
- **17:47:15Z** `SecretPrinter.Probe`, run from the clone on FIOS-STB-01 with
  `--interface 192.168.12.163 --timeout 5`: `No replies were received.`, exit
  code 1.
- **17:49:34Z** the signed probe on the test machine, from its own address on
  the same network: exit code 0, two replies from 192.168.12.180, five
  `INSTANCE` lines. **The printer was answering.** It had also answered the test
  machine's service at 17:20:27Z and 17:39:14Z.
- **17:57:39Z**, on the test machine: the route `192.168.12.0/24` on `Wi-Fi`
  with next hop `0.0.0.0`, and `Find-NetRoute` for the printer chose `Wi-Fi`
  from 192.168.12.142.
- **17:58:44Z**, on FIOS-STB-01: the same route was in the table,
  `192.168.12.0/24` on `Wi-Fi` with next hop `0.0.0.0`, and `Find-NetRoute` for
  the printer chose `Ethernet` from 192.168.1.161 by `0.0.0.0/0`, next hop
  192.168.1.1.
- **18:00:02Z** `ping`, with FIOS-STB-01's printer-side address as the source,
  to that network's gateway and to the printer: `Reply from 192.168.12.163:
  Destination host unreachable.` to both, twice each. The reply's source is
  FIOS-STB-01's own address. The neighbor table read in the same command gave
  `Unreachable` for the printer and `Incomplete` for the gateway.
- **18:01:27Z** `netsh wlan show interfaces`: `State : connected`, 802.11n,
  channel 3, 72.2 Mbps both ways, signal 88%. The last twelve events in the
  `Microsoft-Windows-WLAN-AutoConfig/Operational` log were between 14:32:52Z
  and 14:41:39Z: 8000, 11000, 11001, 11010, 11005 and 8001 at 14:32:52Z; 11004,
  11010 and 11005 at 14:34:41Z; and 11004, 11010 and 11005 at 14:41:34Z to
  14:41:39Z. Nothing later. What each number means was not looked up for this
  finding.
- Edwin reconnected `Wi-Fi` by hand. `Get-NetAdapter` then gave 260 Mbps.
- **18:06:56Z** `Test-NetConnection 192.168.12.180 -Port 631` succeeded by
  `Wi-Fi` from 192.168.12.163, and the service was restarted. **18:07:11Z** it
  was accepting print jobs, and a connection from 192.168.1.152 was relayed a
  second later. At about 2:09 pm Eastern Edwin reported that it printed. The
  log for that print was not read.

So from 17:43:27Z to 18:00:02Z, a little over sixteen minutes, Windows reported
FIOS-STB-01's printer-side adapter as up and connected, holding its address,
and nothing on that network answered it, while the printer was answering
another machine on the same network. No wireless event was logged after
14:41:39Z.

- **When it began is not known.** Nothing was read on FIOS-STB-01 between
  17:13:55Z and 17:43:21Z, and its log before the stop was not read for when it
  last heard the printer.
- **Why is not established.**
- **Whether the walk had any part in it is not established.** The only things
  the walk did on FIOS-STB-01 were stopping and starting its service.
- **The service did not pick the printer up while the link was dead,** and it
  was not given the chance afterwards. It was restarted after the reconnect so
  as not to wait for its next scheduled question. Whether it would have picked
  the printer up at that question was not seen.
- **The route Windows chose is on record already.**
  [The finding of 2026-09-27](2026-09-27-the-printer-went-silent-and-the-join-held.md)
  has the default route chosen with the printer side up and the printer
  silent. Here the printer was not silent; the link to it was.

FIOS-STB-01 offered nothing to the client network from 17:13:55Z to 18:07:11Z.
For two stretches of that, 17:20:30Z to 17:28:18Z and 17:39:17Z to 17:41:48Z,
the test machine offered the printer.

## The probe's `ADDRESS` lines

The earlier walk's finding said the probe's `ADDRESS` line had not been looked
at, though `operating.md` tells the reader to take the printer's address from
it. Read at 17:49:34Z, from the signed probe in the installed folder:

- 131 lines, exit code 0; round 1 brought 1,373 bytes, 5 answers and 11
  additionals, naming 4 instances; round 2 brought 1,305 bytes, 7 answers and 4
  additionals; `Summary: 2 repl(ies), 16 record(s), 5 service type(s) seen.`
  The same figures as in the earlier walk.
- **Under each of the five `INSTANCE` lines there are four `ADDRESS` lines,**
  each `EPSON000000.local -> ` and an address: the IPv4 address,
  192.168.12.180, first, and then three IPv6 addresses.

The document said "the `ADDRESS` line", as if there were one. It now says there
is a line for each address and to use the one that shows an IPv4 address.

## Seen on the iPhone

- At the print of 1:24 pm Eastern, through the test machine, Edwin reported two
  rows in the printer list, and that after he tapped SecretPrinter the print
  screen showed the Epson's name. The page printed.
- At 2:09 pm Eastern, with FIOS-STB-01 the proxy again, he sent two
  screenshots of the same thing and said it still printed. Under "Known
  Printers" the list has `SecretPrinter (ET-3760)`, with the note
  `SecretPrinter proxy. Capabilities read from the printer at 2026-10-06
  18:07:07Z`, and `EPSON ET-3760 Series`. The options screen shows
  `Printer  EPSON ET-3760 Series`; by his account that is what it showed after
  he tapped the first row.

This is the behaviour
[the finding of 2026-09-29](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md)
records, and
[the finding of 2026-09-21](2026-09-21-a-second-printer-entry-on-the-iphone.md)
before it. What today adds: it happened with the test machine's installation,
whose configuration has a different UUID from FIOS-STB-01's, as well as with
FIOS-STB-01's. In the test machine's log every connection of that print was
relayed. Why iOS does it is still not established.

Neither the README nor `operating.md` tells someone installing SecretPrinter
that they will see this. That is not corrected here; it is raised for the README
review that precedes a release.

The screenshots are not in this repository. They show the page being printed.

## What this shows

- **Replacing the program folder updates an installed service.** With the
  service stopped, the old folder removed and the new one copied to the same
  path, the service started from the new files under its existing registration.
  The version stamp and the signatures, read before the start, said which
  build was installed.
- **A build signed by the release workflow runs as a Windows service,** on a
  machine with no .NET installed, with a configuration made by hand, and
  prints.
- **`start= auto` starts the service when Windows boots,** seven seconds after
  the boot time on this machine, on this one occasion.
- **A service started with its printer side unusable waits and then offers the
  printer by itself.** Twice: started by hand with the adapter disconnected,
  and started at boot with the adapter holding no address. Each time it
  announced three seconds after it logged the adapter as usable.
- **Following "Uninstalling" as it stood left the program folder, the data
  folder and its permission on the machine.** It left no service, no registry
  key for one, and no firewall rule.
- **The signatures of all 202 files read `Valid` thirteen and a half hours
  after the signing certificate expired,** and all carry timestamps.
- **The version stamp of a build from the release workflow names its version
  and its commit.**

## The faults in the instructions

All are corrected in `operating.md` in the commit that carries this finding.

1. **There were no update steps for a release.** There are now, under "Updating
   an installed service", beside the steps for an installation built from
   source.
2. **"Uninstalling" did not remove the install.** It gave the firewall rules
   and the log. The command that removes the service's registration was in
   another section and was not referred to; the program folder and the data
   folder were not mentioned. Its sentence "The service leaves nothing else
   behind" is true of what the service writes and was easy to read as true of
   the installation.
3. **The probe prints an `ADDRESS` line for each address,** four for the
   development printer, and the document said "the `ADDRESS` line".
4. **`sc.exe stop` on a stopped service prints a failure,** error 1062. The
   document gave the command with no word on that. It now says so.

Two more corrections are to records, not instructions:

- **The commit named for the folder of 2026-10-04,** in that finding and in
  `verification.md`, as above.
- **A limit in the `REQ-DIST-011` entry of `verification.md`** still said "no
  release workflow has run the script". The release workflow ran
  `publish-release.ps1` on 2026-10-04, for the trial tag `v0.1.0-rc.2`.

## What this does not show

- **No release exists.** Neither folder was a release. The old one was the
  unsigned folder built by hand on 2026-10-04; the new one was the trial
  `0.1.0-rc.2`. So this was not an update from one release to the next.
- **The document's update steps followed from the document.** See "What
  'following the instructions' means here".
- **An install from nothing on a signed folder.** The service's registration
  and the two firewall rules were those made on 2026-10-04. `sc.exe create` and
  `New-NetFirewallRule` were not run in this walk. The test machine now has no
  installation, so the next install there will be from nothing.
- **Going back to the previous version.** The document now says how. It was not
  tried.
- **A boot at which the client-side adapter is not ready.** It had its address
  when the service read it. What the service does otherwise at boot is not
  measured.
- **A boot with the printer side already connected,** and more than one boot,
  one machine.
- **The account the service's process ran under.** The registration was read;
  the process was not.
- **A file downloaded by a browser,** as in the earlier walk.
- **A person working from the document alone.**
- **The uninstall on a machine whose rules have the document's names.**

## Also seen, not explained

- **Two connections have no ending line:** the one accepted at 17:24:37Z, open
  at the stop of 17:28:18Z, and the one from 192.168.1.152 accepted at
  17:39:18Z, open at the stop of 17:41:48Z. Whether `REQ-OBS-006` covers a
  connection open at a stop is still open.
- **IPv4 questions arrived in number in the first run:** 1,418 over IPv4 and 64
  over IPv6 in the 7 minutes 48 seconds the printer was offered. The earlier
  walk, on this machine thirteen hours before, saw 1 and 18 in 1 minute 48
  seconds. In the second run of this walk, 4 and 4 in 2 minutes 31 seconds.
- **A connection from 192.168.1.152 as soon as the listeners opened,** on the
  test machine at 17:39:18Z and on FIOS-STB-01 at 18:07:11Z, with no print
  asked for.

## What Claude predicted wrongly

- **That the installed folder's stamp would name `26ddfbe`.** It named
  `0ae262d`. See above.
- **That the first start would reach `Announced` within about five seconds.**
  The service was waiting for an adapter nobody had connected. Claude's filter
  for that reading left out the `Waiting` line, so the first reading showed
  only `SecretPrinter starting.`, and a second reading was needed.
- **That the two `Accepting print jobs` lines would come before `Announced`.**
  Claude said it was not certain. `Announced` is logged first.
- **That FIOS-STB-01's service would be accepting again fifteen seconds after
  its start,** and then, when it was not, that the printer had probably
  answered after a retry or two, as on 2026-09-27. It had not.
- **A filter meant to keep the printer's addresses out of a paste replaced only
  link-local IPv6 addresses.** Two other IPv6 addresses of the printer were
  shown in the conversation. They are not in this repository.

## Left on the test machine

No SecretPrinter installation: no service, no firewall rule, no program folder
and no data folder. `<walk folder>` holds the zip and its `.sha256` file, the
first unpacked folder with the configuration made by hand in it, the second
unpacked folder, the earlier walk's log, and the copy of the installed
service's log.
