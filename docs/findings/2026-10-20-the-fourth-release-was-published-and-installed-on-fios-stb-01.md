# The fourth release was published and installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-20. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-20 Edwin West chose the version number
0.4.0, pushed the tag `v0.4.0` on commit `22b5252` and approved the signing
job, and the release workflow published SecretPrinter 0.4.0, marked Latest. It
is the first release to log its own version at every start (README
`REQ-OBS-011`), and the first to carry two corrections: a connection accepted
in the moment the service stops or withdraws the printer is closed
(`REQ-LIF-002`), and five warnings no longer end a sentence twice. It is also
the first whose notes end with the section the workflow adds, "Built with",
which names .NET SDK 10.0.402 and the runtime Microsoft.NETCore.App 10.0.13.
On FIOS-STB-01 a copy downloaded with `curl.exe` passed both checks of
[`operating.md`](../operating.md), carried .NET 10.0.13 and the stamp of the
tagged commit, was the same as the zip file by file, and was installed in place
of release 0.3.0 by the "From a release" update steps with the signature
check: **the first update from one release to the next.** The household had
no printer for 10 seconds. The first line the new service logged was its
version, the first time `REQ-OBS-011` ran under the service control manager.
An iPhone printed a page through it. No code changed after the tag.**

*(Status note, 2026-10-21, by Claude, Claude Opus 5.5: 0.4.0's first day on
FIOS-STB-01 was read there on 2026-10-21, read-only, from the service's log,
Windows' WLAN-AutoConfig log and the log of Edwin West's script `Check-SP.ps1`,
from 0.4.0's start to 20:17:02Z. Neither Windows nor the service restarted: the
version line is in the log once, at the start. The service lost the printer six
times and found it again each time without a restart, offering it again 2 or 3
seconds after it answered. It did not offer the printer for 5 hours 56 minutes
in all, 25% of the 23 hours 46 minutes from 20:30:50Z, when it began accepting
print jobs. No error was logged, and no warning but those of the losses and one
`Could not reopen the printer-side socket`, at 00:00:50Z on 2026-10-21, in the
seconds the script had the printer-side Wi-Fi disconnected; the socket was
reopened 7 seconds later. No `Printer lookup failed` line was logged, so the
correction of `1322a06` has still not been seen on a machine. Nearly all of the
time without the printer, 5 hours 50 minutes in two losses, was one outage of
the printer itself. The script's test of the printer's port 631 failed at all
31 of its runs begun from 23:10:27Z on 2026-10-20 to 04:10:27Z on 2026-10-21,
and the gateway answered its ping at 30 of them. Within that, the service found
the printer once, at 23:52:04Z, 5 seconds after Windows reconnected the Wi-Fi
from a drop, and lost it again at 00:00:36Z. Edwin restarted the printer at
about 12:15 am Eastern by his recollection, about 04:15Z, and said that before
it "was not answering on either network". The port answered at the script's run
begun at 04:20:28Z; the service found the printer at 05:10:36Z and offered it
at 05:10:38Z, 50 to 60 minutes after the port answered again. After a loss the
service asks after 1, 2, 4… seconds, up to once an hour (`REQ-RES-008`), and
does not log those questions; whether the printer would have answered one
before 05:10:36Z is in neither log. The same day's Wi-Fi is in a status note on
[the finding of 2026-10-20 on the
Wi-Fi](2026-10-20-windows-reconnected-the-printer-side-wi-fi-by-itself.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name or the printer's certificate fingerprint.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
the commands run on FIOS-STB-01, or from GitHub's pages. Times in am or pm are
US Eastern, UTC-4. Everything here happened on 2026-10-20.

## What went into it

Twelve commits came after the one `v0.3.0` names, up to and including the one
`v0.4.0` names. Three changed what the program does:

- `4fcc475`, of 2026-10-18: the service logs its program and version first at
  every start (`REQ-OBS-011`).
- `6f6c154`: a connection accepted as the service stopped or withdrew the
  printer is closed ([the
  finding](2026-10-20-a-connection-accepted-as-the-relay-stopped-was-left-open.md)).
- `1322a06`: five warnings no longer end a sentence twice (the status note on
  [the finding of
  2026-10-09](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md)).

`434d834` changed how a release is made: the release workflow names the SDK
that built a release, and the runtime its folder carries, in the release notes
and on the run's page. `22b5252` set the version. The other seven changed
documents (`1a637d2`, `5f37525`, `0db40d3` and `767e165`), a comment in
`IppRelay.cs` (`3444b06`), the CI workflow (`6689474`) and what SpecCheck
prints (`6c51848`); none of them changed what the program does.

None of the three changes had run as an installed service, or on a network,
before the release, as the 0.4.0 entry of `CHANGELOG.md` says.

## The commit before the tag

**`22b5252`, the version.** Claude asked Edwin for the version number,
recommending 0.4.0: `CHANGELOG.md` says versions follow Semantic Versioning,
and since 0.3.0 the service had gained a behaviour (`REQ-OBS-011`) beside the
two corrections; 0.3.1 would have described it as fixes only. Edwin chose
"0.4.0 (Recommended)". The commit set `<Version>` to 0.4.0 in
`Directory.Build.props`, added the 0.4.0 entry to `CHANGELOG.md`, a row for
0.4.0 to the table of versions in `SECURITY.md` and a sentence on the fault
0.4.0 corrects to the rows for 0.3.0, 0.2.0 and 0.1.0, and a sentence in four
places saying 0.4.0 is the fourth version meant as a release, each worded so
that it was true before and after publishing. No code changed besides the
version.

On the development machine, before the commit, Edwin measured: Git's view
(five files, 126 lines added, 13 removed), 536 tests passed (28, 27, 47, 62,
44, 205 and 123), and SpecCheck 107 requirements, 23 assemblies, 18 evidence
rows, 542 test records, 91 requirements with a passing test, 90 covered by
code, 0 binding gaps, 0 marked not run, 0 stale, exit code 0. A fresh clone
after the push matched Claude's copy in all 232 files, and the message too. CI
run #157 passed in 1 minute 17 seconds with no annotations.

Before the commit, the step of `release.yml` that checks a tag against the
version and the changelog was run in Claude's workspace, as written, with
PowerShell 7.4.6: for `v0.4.0` it passed, and the new step "Name the SDK and
the runtime", run after it on a made-up folder, gave 95 lines of notes; for
`v0.3.0` it refused, saying the tag and `Directory.Build.props` disagree.

Claude then asked whether to publish, recommending it. Edwin: "Publish now
(Recommended)". He created the annotated tag `v0.4.0` on the commit, named by
its full hash, with the message "SecretPrinter 0.4.0, the fourth version meant
as a release.", pushed it, and printed the commit it names:
`22b5252ee16e589bb7159bea2c3a7fe27b49dc42`. He approved the signing job when it
waited.

## The run that published it

Release run #7, read from the run's public page:

| | |
| --- | --- |
| Result | Success, 5 min 15 s: build 2 min 12 s, sign 1 min 43 s, publish 14 s |
| Approval | by `edwest19`, environment `release`, comment "Reviewed" |
| Kept | `build-record` 31.5 KB, `signed-release` 35.7 MB, `unsigned-folder` 35.6 MB |
| Annotations | one notice, "Built with": ".NET SDK 10.0.402. The folder carries Microsoft.NETCore.App 10.0.13." |

**The notice is the first run of the step added in `434d834`,** and it was
readable on the run's page without signing in, before the signing job had
been approved. Until this run, which SDK built a release was in the job's log
only, which needs a sign-in; the SDKs that built 0.1.0, 0.2.0 and 0.3.0 were
not read.

The release page, read without signing in: 0.4.0, marked **Latest** and not a
pre-release, on commit `22b5252`, released by `github-actions` at 20:17Z, with
the 0.4.0 entry of `CHANGELOG.md` as its notes, followed by the section "Built
with": ".NET SDK 10.0.402, as `dotnet --version` named it on the build
machine." and "The .NET runtime in the folder: Microsoft.NETCore.App 10.0.13,
as `SecretPrinter.Service.runtimeconfig.json` names it.", then the sentence
saying the section is added by the workflow and is not in `CHANGELOG.md`. Its
two files, both uploaded at 20:17:43Z:

| File | Size | SHA-256 as GitHub shows it |
| --- | --- | --- |
| `SecretPrinter-0.4.0-win-x64.zip` | 35.8 MB | `f9fe275c7cd4b5bc24c1897c3c0f94e8fcb701040c2462a9b76b574961a4bb5f` |
| `SecretPrinter-0.4.0-win-x64.zip.sha256` | 99 bytes | `68d8ed2a8dc156eb74405a7be23d72faeee9934e63144905edd26b4283b077b8` |

## The runtime it carries

Microsoft's release metadata for .NET, read that afternoon before the version
commit, gave 10.0.13 of 2026-10-13 as the latest .NET 10 release, with SDK
10.0.402: the runtime 0.3.0 carries. The run named SDK 10.0.402 and runtime
10.0.13, and the downloaded copy carries `Microsoft.NETCore.App 10.0.13`. The
one package the service declares, `System.ServiceProcess.ServiceController`,
stays at 10.0.11; the project file that names it did not change since 0.3.0.

## The checks on FIOS-STB-01

All in Edwin's SSH session on FIOS-STB-01, which runs Windows 10, by the
commands used for 0.3.0 with the version changed.

**The download.** The files were not there before. Both `curl.exe` commands
ended with exit code 0. The zip was 37,582,485 bytes and the `.sha256` 99
bytes, written at 20:23:04Z.

**Check 1, as printed.** `Get-FileHash` gave
`F9FE275C7CD4B5BC24C1897C3C0F94E8FCB701040C2462A9B76B574961A4BB5F`, and the
`.sha256` file held the same 64 characters followed by the file name. It is
also GitHub's digest above.

**The unpacking.** Into a new folder in Edwin's user folder, by a command that
would have refused, and unpacked nothing, had the folder existed; it did not.
The folder was created at 20:25:06Z.

**Check 2, as printed,** on that folder: 216 files and four lines, every one
`Valid`:

| Files | Ours | Signer |
| --- | --- | --- |
| 12 | yes | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
| 185 | no | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | no | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | no | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |

No program file lacked a timestamp. The folder names its runtime as
`Microsoft.NETCore.App 10.0.13`, and the service's stamp read
`0.4.0+22b5252ee16e589bb7159bea2c3a7fe27b49dc42`, the commit the tag names.

**The folder against the zip.** For each of the zip's 216 files, the SHA-256 of
the file inside the zip and of the same file in the folder: **all 216 were the
same, none was missing, and the folder held no other file.**

## The install

Claude asked whether to install now, recommending it; Edwin: "Install now
(Recommended)". One command, run in the same session, which is elevated, did
steps 2 to 5 of "From a release" with step 4's signature check, as for 0.3.0:

- It first tested TCP port 631 on the printer, and would have stopped there,
  touching nothing, had the printer not answered. It answered.
- Step 2: stopped the service, removed the program folder, and confirmed it
  gone.
- Step 3: copied the new folder in: 216 files.
- Step 4: ran the signature check on the installed folder, which gave the same
  four lines, and read the stamp. It would have left the service stopped
  unless the stamp was the one above, every program file was `Valid`, and
  SecretPrinter's were signed under the name above. All three held.
- Step 5: started the service and, 20 seconds later, read the log.

The service's registration, configuration, log and firewall rules were not
touched. **What it replaced was release 0.3.0,** installed there on 2026-10-15
by the same steps: this is the first time an installation made from one
release was updated to the next.

| Time | What the service logged |
| --- | --- |
| 20:30:40Z | Release 0.3.0: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 20:30:45Z | **`SecretPrinter.Service version 0.4.0+22b5252ee16e589bb7159bea2c3a7fe27b49dc42`** |
| 20:30:46Z | `Service 'SecretPrinter' starting under the service control manager.`; the configuration; the client interface `Ethernet` (index 19); the printer interface `Wi-Fi` (index 16); the printer's capabilities and connection answered; probing for the advertised names |
| 20:30:47Z | `Probe clear: no other device answered for the advertised names.` |
| 20:30:49Z | `Announced. Answering queries.` |
| 20:30:50Z | Accepting print jobs on the client interface's IPv4 address and on its link-local address |

**The version line is the first line of the start,** a second before the line
that says it started under the service control manager, and its stamp is the
one step 4 read from the installed file. Before this, `REQ-OBS-011` had run
only in tests, one of which starts the program on Windows with a configuration
file that does not exist.

**The household had no printer for 10 seconds,** from 20:30:40Z to 20:30:50Z.
No warning or error was logged.

## What release 0.3.0 said when it stopped

Its stop summary at 20:30:40Z, which counts from its last start:

```
Served 897 quer(ies) of 70380 seen; 69304 were for other services and were ignored; 178 carried every answer already and were not answered (RFC 6762 s7.1); 1 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 750 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 767 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
By transport: IPv4 answered 799 of 44695 seen; IPv6 answered 98 of 25685 seen.
```

897, 69,304, 178 and 1 add up to 70,380, and the two transports to 897 and
70,380. 0.3.0 was installed on 2026-10-15 at 17:16:48Z; when its last start
was, and so the time these counts cover, was not read.

## The print

Edwin tapped Print on the iPhone at 4:35 pm, "and it printed immediately". The
log from 20:30:45Z, read at 20:37:45Z:

- 5 connections accepted, 5 relayed with the printer's certificate matching the
  pinned fingerprint, 5 completed; none failed and none was refused. All came
  from link-local addresses, and they ended between 20:36:09Z and 20:36:42Z,
  33.42 to 85.32 seconds after they began.
- One carried 1,064,475 bytes to the printer and 5,577 back in 55.98 seconds;
  the other four carried between 2,512 and 13,928 bytes to the printer. Which
  of them carried the page is not in the log.
- No warning or error was logged from 0.4.0's start to the reading.

## What Claude predicted, and what it did not

Held: the release metadata's runtime, 10.0.13, and SDK, 10.0.402, as what the
run named; the CI runs with no annotations; the tag check; the commit the tag
names; the download's exit codes; check 1, the published digest and the
`.sha256` file the same; 216 files; check 2's counts, all `Valid`, 12 under the
expected name; no file without a timestamp; the runtime and the stamp; the
folder the same as the zip, file by file; the install's lines, from 0.3.0's
stop to `Accepting print jobs`, with the version line first; 10 seconds without
a printer; no warning or error; the counts of the print's connections being
equal, none failed. The zip's size, the folder's creation time and the numbers
in the stop summary were said in advance to be unknown.

Not predicted:

- **Check 2's lines came in another order** than Claude gave: 185, 2, 3 for
  Microsoft's three names, where Claude said 185, 3, 2. The counts were as
  predicted.
- **The print:** Claude said about six connections, each lasting 30 to 80
  seconds. There were five, and the longest lasted 85.32 seconds.
- **Caught before it was sent:** the tag command's last part, which prints the
  commit the tag names, was first written as `git rev-parse v0.4.0^{commit}`.
  Tried in Claude's workspace, PowerShell passed `{commit}` to the program as
  a script block, not as text; the command sent quoted it.

## Sentences the release made untrue

Three sentences Claude wrote earlier that day, in `434d834`, said the step
that names the SDK had not yet run on GitHub: in the header of `release.yml`
("Not yet run on GitHub: the next tag is its first run."), in the preamble of
`CHANGELOG.md` ("No release has carried that section yet.") and in the
`REQ-DIST-006` row of `docs/verification.md` ("Not yet run on GitHub."). All
three became untrue at the tag. The sentences on the version in `22b5252` had
been worded to be true before and after publishing; these three were not, and
that is Claude's mistake. A fourth, in `docs/operating.md` under "Updating an
installed service", said "An update from one release to the next has not yet
been made." and became untrue at the install. The commit that adds this
finding corrects all four, each with a note giving what it said.

## What this does not show

- **0.4.0 over time.** One start and one print, within minutes of the install.
  *(Status note, 2026-10-21, by Claude, Claude Opus 5.5: its first day was read
  on 2026-10-21; see the status note at the top.)*
- **A Windows shutdown or restart with 0.4.0 running,** or a start of 0.4.0 at
  boot.
- **The moment `REQ-LIF-002`'s correction is about:** a connection accepted as
  the service stops or withdraws the printer. It cannot be made to happen on
  demand.
- **0.4.0 on Windows 11, or on a machine with no .NET.** FIOS-STB-01 has .NET
  SDKs installed; the release carries its own runtime.
- **A download with a browser.** Both checks were run on a copy `curl.exe`
  fetched.
- **Which of the CVEs Microsoft lists for .NET 10.0.13 concern code
  SecretPrinter runs.** The runtime is the one 0.3.0 carries.

## How it was read

Commands given to Edwin, each read-only except the install, run by him in an
SSH session on FIOS-STB-01 or in PowerShell on the development machine, and
pasted or attached. GitHub's run and release pages were read without signing
in, through a tool that fetches a page and returns a model's reading of it.
Microsoft's release metadata was read the same way. The commands masked
link-local and other IPv6 addresses, IPv4 link-local addresses, the printer's
host name and ids; the machine's and the printer's addresses on both networks,
and the printer's certificate fingerprint, were in what was pasted and are not
repeated here. The arithmetic was done in Claude's workspace.
