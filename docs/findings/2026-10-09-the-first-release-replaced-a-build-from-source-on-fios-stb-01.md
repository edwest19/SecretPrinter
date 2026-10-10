# The first release replaced a build from source on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-09. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-09 FIOS-STB-01, the household's proxy,
was moved from a build made from source at commit `64e8fc2` to release 0.1.0,
by the steps under "Updating an installed service", "From a release", in
[`operating.md`](../operating.md). The published zip was downloaded on
FIOS-STB-01 and passed both checks there. The program folder was removed and
the release folder copied in its place, its signatures and version stamp were
read before the start, and the service started under its existing
registration, configuration and firewall rules, found the printer, probed,
announced, and accepted print jobs. An iPhone printed a page through it. This
is the first run of the 0.1.0 build, and, by the record in these findings, the
first run of a self-contained release folder on Windows 10. During the update
the printer was not offered for 10 minutes 12 seconds. The steps worked as
printed. The document
had not said that they also serve an installation built from source, nor how
such an installation goes back, nor how to download on a machine reached only
by SSH; those are corrected in the commit that carries this finding. No code
changed.**

*(Status note, 2026-10-10, by Claude, Claude Opus 5.5: 0.1.0 has since been
seen losing the printer and finding it again on FIOS-STB-01, which this
finding listed as not seen. In its first 22 hours 14 minutes it lost the
printer 14 times and found it again each time without a restart. In four of
those losses a script of Edwin's, not part of SecretPrinter, had reconnected
the printer-side Wi-Fi. See
[the finding of 2026-10-10](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md),
which also records what came of it: the questions now start over when the
adapter is usable again.)*

*(Status note, 2026-10-10, later the same day, by Claude, Claude Opus 5.5:
FIOS-STB-01 no longer runs 0.1.0. At 21:52:55Z a build of `5db9063`, made on
FIOS-STB-01 by `publish-release.ps1`, carrying .NET 10.0.12 and not signed, was
installed in its place by the steps this finding followed, without the
signature check, to run overnight before a release. An iPhone printed through
it. See
[the finding of the install](2026-10-10-the-start-over-build-was-installed-on-fios-stb-01.md).)*

No IPv6 address, MAC address or device name appears in this finding, and
neither do the network names. The printer's host name is written
`EPSON000000`. The folder the release was unpacked into is written `<walk
folder>`, and the UUID in the configuration is not given.

**About the times.** Times ending in `Z` are UTC and come from FIOS-STB-01's
clock, the commands' own output or the service's log. Times in pm are US
Eastern and say whose clock they are. The update ran from 17:07Z to 17:44Z,
which is 1:07 pm to 1:44 pm Eastern.

## Why this was done

- **The household's proxy ran a build that was never released.** FIOS-STB-01
  had run `64e8fc2` since 2026-10-02, built from its own clone with the
  "From source" steps. Release 0.1.0 was published on 2026-10-08
  ([finding](2026-10-08-the-first-release-was-published.md)), and nothing in
  that build had been run.
- **No release folder had run on Windows 10.** Every run of one on record was
  on the test machine, which runs Windows 11
  ([2026-10-04](2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md),
  [2026-10-06](2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md),
  [2026-10-08](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md)).
  README open question 10 records that Microsoft's list of the systems .NET 10
  supports leaves out Windows 10 22H2.
- **The update steps for a release had not been followed from the document.**
  They were written on 2026-10-06 after being run from commands given in the
  conversation, and the walk of 2026-10-08 ended in an uninstall, leaving
  nothing to update.

Before the first step, what would complete the work was stated: the zip on
FIOS-STB-01 passes both checks; the installed folder's signatures and stamp are
read before the start; the service starts, offers the printer, and an iPhone
prints through it; and a finding records every place where the document and the
machine disagreed. All of it was done.

It was also said before the first step that the document was not written for
this case. "Updating an installed service" begins: "There are two procedures:
one for an installation made from a release folder, and one for an
installation built from source." FIOS-STB-01's was built from source, and the
steps followed were the ones for a release.

## What "following the document" means here

Edwin ran every command, in an SSH session to FIOS-STB-01 in Windows PowerShell,
under an account with administrator rights; the session read as elevated.
Claude gave the commands one at a time and said what it expected before each.

- **The document's commands were run as printed,** with their placeholders
  filled in: the version, `<new folder>`, `<folder>` and
  `<new-release-folder>`.
- **Readings between the steps** were Claude's own commands, each read-only.
  They are not in the document.

Where a command differed from the document:

| Step | Difference |
| --- | --- |
| Getting the release | The document says to download the two files. FIOS-STB-01 has no one at its screen, so they were downloaded in the SSH session with `curl.exe`, the copy of curl that comes with Windows. |
| Unpacking | Preceded by a `Test-Path` that the new folder did not exist. |
| Check 2 | Run once on the unpacked folder and once on the installed folder, as the document asks, each time followed by a reading of the runtime the folder carries. |
| Stopping and removing | `Stop-Service`, `Remove-Item` and `Test-Path` on one line, with the time before and the service's state and the end of its log after. |
| Copying and confirming | `Copy-Item`, a file count, a `Test-Path` on the program, check 2, the runtime and the stamp, on one line. |
| Starting | `Start-Service` and a 15-second wait as the document says; then, instead of its `Get-Content ... -Tail 5`, every line logged since the start, with the lines that list the advertised records left out and addresses, the printer's host name and UUIDs replaced. It shows every line the document's command would, except any that list the advertised records. |

## The machine before the update

Read at 17:22:40Z and in the minutes after, before anything was changed.

- **The service:** `sc.exe qc` gave `START_TYPE : 2 AUTO_START`, the program
  path `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe` in quotes
  followed by `--config`, `--log-file` and `--service` naming files in
  `C:\ProgramData\SecretPrinter`, and `SERVICE_START_NAME : NT
  AUTHORITY\LocalService`: the registration measured on 2026-09-21
  ([finding](2026-09-18-running-as-localservice.md)). It was `Running`.
- **The firewall rules:** found by the program they name, not by their names.
  There were two, `SecretPrinter mDNS` and `SecretPrinter IPP`, both enabled,
  inbound, `Allow`, `Private`, on 5353 and 631, both naming
  `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`.
- **The configuration** names `Ethernet` as the client interface and `Wi-Fi` as
  the printer's side, and holds a UUID of its own, not the example's old fixed
  one ([finding](2026-10-07-the-example-configuration-handed-every-user-the-same-uuid.md)).
  The only change between `64e8fc2` and 0.1.0 in what the configuration
  loader accepts is that an empty UUID is refused, so 0.1.0 would load this
  file unchanged. The data folder held the configuration and the log, and
  nothing else.
- **The adapters:** `Ethernet` up at 1 Gbps, `Wi-Fi` up at 54 Mbps, and
  `Test-NetConnection` to the printer's port 631 succeeded through `Wi-Fi`.

### The old program folder

Read before it was removed, because nothing else could say what it held.

- **26 files:** SecretPrinter's nine assemblies, each with its `.pdb`;
  `SecretPrinter.Service.exe`, `.deps.json` and `.runtimeconfig.json`;
  `System.ServiceProcess.ServiceController.dll` and
  `System.Diagnostics.EventLog.dll`; and under `runtimes\win\lib\net10.0\`
  those two again and `System.Diagnostics.EventLog.Messages.dll`. No
  configuration, no log, nothing that is not a build output. This list was
  predicted from the project files at `64e8fc2` and held.
- **`SecretPrinter.Service.exe` read `NotSigned`,** as a build made on the
  machine itself should.
- **The stamp** in `SecretPrinter.Service.dll` read
  `1.0.0+64e8fc2df5e3d89a13c24241eee4e7c73a78a6a0`: the commit on record, and
  `1.0.0` because no version was set in the repository until `650f60c`.
- **`SecretPrinter.Service.runtimeconfig.json`**, read whole: a `framework`
  entry naming `Microsoft.NETCore.App` version `10.0.0`, and no
  `includedFrameworks`. So this folder ran on whichever .NET 10 was installed
  on the machine, as `operating.md` says the "From source" procedure installs.
  `10.0.0` is the lowest version it accepts, not the one that ran. Its
  `configProperties` held `System.Globalization.Invariant` and
  `System.Globalization.PredefinedCulturesOnly` true, and
  `System.Reflection.Metadata.MetadataUpdater.IsSupported` and
  `System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization`
  false.
- **Times written:** SecretPrinter's assemblies, their `.pdb` files and the
  `.exe` at 16:04:10Z to 16:04:16Z on 2026-10-02, after `64e8fc2` was
  committed; Microsoft's files on 2026-07-24. **`SecretPrinter.Service.deps.json`
  and `SecretPrinter.Service.runtimeconfig.json` read 2026-10-01 14:30:01Z,**
  before `64e8fc2` existed. That was not predicted, and why is not
  established. It does not bear on the update: the stamp in the files that run
  names `64e8fc2`, and the whole folder was replaced. The clone's own build
  output is still on FIOS-STB-01 if it is wanted.

## The release, on FIOS-STB-01

- **The download,** at 17:07:50Z, into the account's Downloads folder:

  ```powershell
  curl.exe --fail --location --silent --show-error --output "$d\SecretPrinter-0.1.0-win-x64.zip" $u
  ```

  with `$d` the Downloads folder and `$u` the zip's address on the Releases
  page, and the same for the `.sha256` file. Both exited 0. The zip is
  **37,557,300 bytes**, the first time its length in bytes has been read; the
  `.sha256` file is 99 bytes, as GitHub shows.
- **No internet mark.** Each file held only its `:$DATA` stream: no
  `Zone.Identifier` and no `SmartScreen` stream, where the pre-release's zip,
  downloaded with a browser on the test machine, had both
  ([finding](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md)).
- **Check 1, as printed:** `Get-FileHash` gave
  `7006676752EE2C767D264EAB810E835F1D56A4FF88F19DE89D277994DA209D22`, and the
  `.sha256` file holds the same, two spaces and the zip's name.
- **Unpacked** with the document's `Expand-Archive` command into `<walk
  folder>`, which did not exist before: 216 files.
- **Check 2, as printed,** the first time on Windows 10: four lines, every one
  `Valid`. 185 signed `CN=.NET`, 2 `CN=Microsoft Corporation` and 3
  `CN=.NET DAC`, all `O=Microsoft Corporation, L=Redmond, S=Washington, C=US`;
  and 12, SecretPrinter's own, signed
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`.
- **The runtime** in its `runtimeconfig.json`: `includedFrameworks`,
  `Microsoft.NETCore.App 10.0.12`. **The stamp:**
  `0.1.0+27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d`, the commit the tag `v0.1.0`
  names.

## The update

1. **Stop and remove,** at **17:29:18Z**. `Stop-Service` printed nothing, and
   neither did `Remove-Item`. `Test-Path` printed `False`; the service read
   `Stopped`. Its log, all at 17:29:18Z: `Service 'SecretPrinter' stopping.`,
   `No longer accepting print jobs on Ethernet.`, `Advertisement retracted
   (goodbye records sent).`, `Served 3 quer(ies) of 185 seen; 182 were for
   other services and were ignored.`, `By transport: IPv4 answered 0 of 61
   seen; IPv6 answered 3 of 124 seen.` and `SecretPrinter stopped.` From here
   the household had no printer on the client network.
2. **Copy and confirm,** at 17:37:22Z. Between the stop and this step Edwin
   raised whether to remove the .NET SDK from FIOS-STB-01 there and then, and
   decided to leave it. `Copy-Item` printed nothing. 216 files;
   the program at the path the service is registered with; check 2 the same
   four lines, every one `Valid`; `Microsoft.NETCore.App 10.0.12`; and the
   stamp `0.1.0+27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d`, whose part before
   the `+` is the version downloaded, as step 4 requires.
3. **Start,** at 17:39:25Z. Fifteen seconds later the service read `Running`.
   Its log from 17:39:26Z: started under the service control manager; the
   configuration's settings; the printer interface `Wi-Fi` resolved at once;
   the resolver socket joined on `Wi-Fi` only; the responder's sockets for
   both address families on `Ethernet`; the printer found at 192.168.12.180;
   its link-local address published in `AAAA` records; `Probing for the
   advertised names` at 17:39:27Z and `Probe clear` the same second;
   `Announced. Answering queries.` at 17:39:29Z; and at **17:39:30Z**
   `Accepting print jobs on 192.168.1.161:631 (Ethernet); permitted clients:
   192.168.1.0/24.` and the same on its link-local address. No warning and no
   error. The start-up ended as step 5 of the document says it should.

The registration, the configuration, the log and both firewall rules were not
touched, and none needed to be: each names a path the update left where it was.
That is what step 2 of the document says, and here it held for an installation
built from source.

## How long the household had no printer

From the stop at 17:29:18Z to the first `Accepting print jobs` line at
17:39:30Z: **10 minutes 12 seconds**. Each command took seconds; the rest was
time between them, 8 minutes 4 seconds of it between the stop and the copy.
The walk of 2026-10-08, which did more, cost 34 minutes 40 seconds.

## The print

From the iPhone, between 1:41 and 1:42 pm by its clock, one page. Edwin: "yes
it printed." He sent four screenshots, which show the document and are not in
this repository:

- **The list:** under **Known Printers**, `SecretPrinter (ET-3760)` and `EPSON
  ET-3760 Series`, as `operating.md` records for 2026-09-21, 2026-09-22,
  2026-09-25 and 2026-09-29. On 2026-10-08, through an installation whose UUID
  was generated that day, the proxy was under Other Printers.
- **The note** beside the proxy read "SecretPrinter proxy. Capabilities read
  from the printer at 2026-10-09 16:11:08Z". See the next section.
- **After picking the proxy,** the options screen showed the printer as `EPSON
  ET-3760 Series`, as on every earlier occasion.
- **The print queue** showed the job "Printing 1 of 1, EPSON ET-3760 Series".

**The log,** read at 17:47:13Z: eight connections from the iPhone's link-local
address, the first accepted at 17:41:33Z and the last completed at 17:43:40Z.
Each was relayed to 192.168.12.180:631 with "encryption to printer: TLS 1.2,
certificate matched the pinned fingerprint", and each has its `completed`
line. None failed and none was refused. The page went in the connection opened
at 17:42:18Z, which carried 1,065,220 bytes to the printer in 82.01 seconds;
the other seven carried between 657 and 8,015 bytes each. No client connected
over IPv4.

## The time in the note

The note gave 16:11:08Z. Release 0.1.0 read the printer's capabilities at its
start, at 17:39:26Z, and the iPhone showed the list about two minutes after it
announced.

- **16:11:08Z is when the build from source last started.** Its log shows
  `Service 'SecretPrinter' stopping.` and `SecretPrinter stopped.` at
  16:11:07Z, `SecretPrinter starting.` at 16:11:08Z and `Announced. Answering
  queries.` at 16:11:12Z. Windows had last started on 2026-10-08 at 00:29:01Z,
  so this was not a boot. Edwin had restarted the service; see the next
  section.
- **The note's time is fixed when the service starts.** Read in the code:
  `ServiceHost` builds the advertisement once, from the capabilities read at
  start, and when the printer is lost and found again `Offering` announces the
  same records once more. Neither file changed between `64e8fc2` and 0.1.0. So
  the old build's note said 16:11:08Z from that start until its stop at
  17:29:18Z, through the loss and recovery at 17:16Z below, and 0.1.0's says
  17:39:26Z.
- **Why the iPhone showed the old time is not established.** By the code and
  its log, the old build sent goodbye records at the loss at 17:16:24Z and
  again at its stop, and 0.1.0 announced its own records at 17:39:29Z. What
  the iPhone had heard of each is not known; nothing was captured.

So the time in the note is not, on its own, a sign of which installation or
which start is answering.

## Seen on the way: the printer side, under the build from source

The log since the service's start on 2026-10-08 at 21:43:16Z, read for the
lines where it lost or found the printer, started, stopped or warned. On
2026-10-09, before the update, the printer side lost the printer eight times.
While each loss lasted, the printer was not offered on the client network. All
the times in the table are on 2026-10-09.

| | Lost | Answered again | Not offered for |
| --- | --- | --- | --- |
| 1 | 00:40:35Z | 01:15:40Z | 35 min 5 s |
| 2 | 02:35:39Z | 02:53:35Z | 17 min 56 s |
| 3 | 03:24:48Z | 05:33:05Z | 2 h 8 min 17 s |
| 4 | 06:05:57Z | 14:20:57Z, after a restart | 8 h 15 min 0 s |
| 5 | 14:24:32Z | 14:25:30Z | 58 s |
| 6 | 15:09:42Z | 15:14:39Z | 4 min 57 s |
| 7 | 15:57:07Z | 16:11:12Z, after a restart | 14 min 5 s |
| 8 | 17:16:24Z | 17:17:59Z | 1 min 35 s |

"Lost" is the time of the line beginning `Printer unreachable`; "answered
again" that of `Printer reachable again`, or, where the loss ended with a
restart, of `Announced. Answering queries.` after it.

- **In losses 3, 4 and 7 the log says the printer-side adapter had no usable
  address.** Lines beginning `Printer lookup failed before an answer could be
  expected: SocketException: The requested address is not valid in its
  context.` came first (from 03:24:25Z and 15:57:01Z), then `Could not reopen
  the printer-side socket`, saying that `Wi-Fi` held a 169.254 address but was
  not up, or had no IPv4 address. In loss 4 that line came at 13:15:47Z, seven
  hours after the loss began. Loss 3 came back by itself, and so did the five
  in which no such line appears.
- **Losses 4 and 7 ended when Edwin restarted the service.** Each restart is a
  stop and a start within one second: on 2026-10-09 at 14:20:53Z and
  16:11:07Z, and on 2026-10-08 at 19:12:32Z and 19:12:53Z. Asked, he said he restarted it each
  time. His practice, in his words: "i check periodically to see if it is up".
  He reads the adapters, reconnects the printer-side Wi-Fi by hand if it is
  disconnected, checks that the gateway and the printer's port answer, reads
  the end of the log, and restarts the service; if the printer does not
  answer, he restarts the printer. "the printer stops answering and the wi-fi
  disconnects a lot." After both restarts on 2026-10-09 the printer answered
  within seconds.
- **When the link came back** in losses 4 and 7 is not known. The service asks
  on a schedule that backs off to once an hour (`REQ-RES-008`), so a printer
  that returns can go unoffered for up to an hour.
  Edwin decided on 2026-10-02 that the schedule stays as it is for a release
  ([finding](2026-10-03-the-printer-came-back-at-an-hourly-question.md)); these
  losses are a record of how it went on this link, not a change to that
  decision.
- **This explains the start on 2026-10-08 at 19:12:54Z** that the walk of that
  day could not ([finding](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md)):
  it was the second of two restarts 21 seconds apart. In the first, which
  started at 19:12:33Z, the printer did not answer within the first five
  seconds, and the service announced at 19:12:50Z.

## Where the document and the machine disagreed, or the document was silent

Each is corrected in `operating.md` in the commit that carries this finding.

1. **"Updating an installed service" did not cover moving from a build from
   source to a release.** It offered two procedures, by how the installation
   was made. FIOS-STB-01's was built from source, and the release steps
   replaced it without change: the old folder was removed whole, so nothing of
   the framework-dependent build stayed beside the self-contained one, and
   every other part of the install already named the paths the release uses.
   The section now says so, measured once.
2. **"To go back" assumed a previous release.** An installation that was built
   from source has none. Going back means the "From source" steps, but those
   publish over the folder, which never removes a file, so the release's files
   would stay beside the rebuilt ones. The section now says to remove the
   folder first. Going back has still not been tried.
3. **Downloading on a machine reached by SSH.** The document says to download
   the two files and gives no command, which fits a browser. The `curl.exe`
   commands above are now given for a machine without one at hand, with the
   reminder that in Windows PowerShell `curl` without `.exe` is a different
   command, and with what was seen of the internet mark.
4. **Who has followed the update steps.** The document said they were run
   before they were written and that nobody had yet followed them from it.
   They have now been followed from it, one command at a time as Claude gave
   them, so a person working from the document alone is still not shown.
5. **"From source" installs a framework-dependent build.** This was stated and
   not measured. The folder read before the update shows it, and the section
   now says so.

Also added to `operating.md`: the stamp and runtime read on 0.1.0 under step 4;
this update among the results under "Where the program comes from" and
"Checking a release before you install it"; and, under "What you will see on an
iPhone", where the list put the proxy this time and that the note's time is
fixed at the service's start.

Corrected elsewhere, because this update made them out of date:

- **`CHANGELOG.md`:** a dated note under the 0.1.0 entry's "What happened",
  which says that nothing in it had been run. The release notes GitHub shows
  were taken from this file at the tag and do not change.
- **`README.md`:** open question 10 gains this run, the first of a release
  folder on Windows 10; and section 12 gains a paragraph on it.
- **`docs/verification.md`:** this finding added to the evidence for
  `REQ-DIST-005`, the two checks, now run on Windows 10.
- **Status notes** on the two findings of 2026-10-08, which say that nothing in
  the 0.1.0 build had been run and that the start at 19:12:54Z was not
  explained.

## Seen, and not corrected

- **A warning line ends a sentence twice:** `... not valid in its context..
  Treated as no answer.` The message Windows supplies ends with a full stop,
  and `PrinterWatch` adds another. 0.1.0 has the same line. It changes a line a
  release has been measured with, so it waits for the next change to the code.
- **The stop at 19:12:32Z on 2026-10-08 has no `SecretPrinter stopped.` line**
  after `Service 'SecretPrinter' stopping.`, where every other stop in the
  reading has one. What the service was doing before that stop was not read,
  and when the code skips the line has not been read either.
- **No Service Control Manager event since 2026-10-08 names SecretPrinter.**
  The reading looked for any event from that source whose message names the
  service, from midnight Eastern on 2026-10-08, a span with six stops and six
  starts in the service's log. It found none. Why Windows recorded none is not established.
- **Four connections at 16:37:20Z,** completed within 33 seconds, carried 657
  to 3,418 bytes each to the printer. No print was asked for by Edwin's
  account. What they were is not established; connections with no print have
  been seen before, the latest on 2026-10-08.

## What Claude predicted wrongly

- **The last line before the stop.** Claude said it would be at 16:11:12Z,
  because the folder listing gave the log that write time at about 17:27Z. The
  log held lines to 17:18:02Z. The listing's time was not the time of the last
  line written, and the prediction should have come from the log itself. What
  made the listing show 16:11:12Z is not established.
- **Service Control Manager events.** Claude predicted an event for each stop
  and start. There were none; see above.
- **The times the old folder's files were written.** Claude predicted 2026-10-02
  for all of SecretPrinter's own files. Two read 2026-10-01; see above.

Every other prediction made before a command held, among them the old
folder's file list, its stamp and its runtime file, every reading of the
release, and every line of the start-up that was predicted.

## What this does not show

- **Going back.** Not tried. For this installation it would be the "From
  source" steps on an empty program folder.
- **An update from one release to the next.** This was from a build from source
  to the first release.
- **0.1.0 with no .NET installed, on Windows 10.** FIOS-STB-01 still has the
  .NET SDK. Edwin chose to leave it, and to come back to it with the packaged
  service, SecretPrinterX. A self-contained folder is built to use only its own
  runtime; that this one used nothing from the installed one was not measured.
- **A start at boot of 0.1.0.** The registration is `AUTO_START`; Windows has
  not been restarted since.
- **0.1.0 losing the printer and finding it again on this machine.** Not seen
  yet. `PrinterWatch` changed between `64e8fc2` and 0.1.0.
- **A person working from the document alone,** and any client but the one
  iPhone, which connected only over IPv6 link-local.
- **Why the iPhone showed the old build's time in the note.**

## Where it was measured

- **FIOS-STB-01,** Windows 10, reached by SSH from another computer; its
  Windows version was not read this time. README open question 10 records it as
  22H2. Its client side is `Ethernet` at 192.168.1.161 and its printer side a
  USB Wi-Fi adapter, `Wi-Fi`, at 192.168.12.136, connected at 54 Mbps.
- **The iPhone,** by Edwin's account and his four screenshots.
- **The record,** in the service's log on FIOS-STB-01, read with filters that
  replace addresses, the printer's host name and UUIDs.

## Left on FIOS-STB-01

The zip and its `.sha256` in the account's Downloads folder; `<walk folder>`,
holding the unpacked release; the clone, at `64e8fc2` by the record and not
read this time; and the .NET SDK. All are Edwin's to keep or remove.
