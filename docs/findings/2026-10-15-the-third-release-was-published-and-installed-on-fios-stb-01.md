# The third release was published and installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-15. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-15 Edwin West chose the version number
0.3.0, pushed the tag `v0.3.0` on commit `064ca9f` and approved the signing
job, and the release workflow published SecretPrinter 0.3.0, marked Latest. It
is the first release to carry the corrections of README `REQ-SEC-017` and
`REQ-LIF-009`, faults that 0.1.0 and 0.2.0 both have. On FIOS-STB-01 a copy
downloaded with `curl.exe` passed both checks of
[`operating.md`](../operating.md), carried .NET 10.0.13 and the stamp of the
tagged commit, was the same as the zip file by file, and was installed in place
of the unsigned build of `8663e5c` that had run there since 2026-10-14, by the
"From a release" update steps with the signature check. The household had no
printer for 10 seconds. An iPhone printed a page through it. Updating from one
release to the next is still not shown: what 0.3.0 replaced was a build, not
0.2.0. No code changed.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name or the printer's certificate fingerprint.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
the commands run on FIOS-STB-01, or from GitHub's pages. Times in am or pm are
US Eastern, UTC-4. Everything here happened on 2026-10-15 unless another date
is given.

## The two commits before the tag

**`a148817`, CI on Node.js 24.** The CI runs read on 2026-10-13 and 2026-10-14
carried a note from GitHub that `actions/checkout@v4`,
`actions/setup-dotnet@v4` and `actions/upload-artifact@v4` "target Node.js 20
but are being forced to run on Node.js 24". GitHub's changelog set 2026-06-16
as the day its runners would use Node.js 24 by default, and its post of
2026-09-23 says that Node.js 20 "is no longer available on GitHub Actions
runners". Each of the three, at the commit its `v4` tag names, declares
`node20` in its `action.yml`. `.github/workflows/ci.yml` now names them by the
commits `release.yml` already pins (checkout v7.0.1, setup-dotnet v6.0.0,
upload-artifact v7.0.1), each of which declares `node24`. Every action
`release.yml` uses declares `node24`; the signing action is a composite whose
one inner action, `actions/cache` v5.0.4, does too. The bridge to the
development machine refused to write a file under `.github\workflows` ("a
protected file"), so Edwin placed it by hand. CI run #144 passed in 1 minute 36
seconds with no annotations.

**`064ca9f`, the version.** Claude asked Edwin for the version number,
recommending 0.3.0: `CHANGELOG.md` says versions follow Semantic Versioning,
and since 0.2.0 the service had gained three behaviours (`REQ-ADV-027`,
`REQ-ADV-028`, `REQ-ADV-029`) beside the two corrections. Edwin chose "0.3.0
(Recommended)". The commit set `<Version>` to 0.3.0 in `Directory.Build.props`,
added the 0.3.0 entry to `CHANGELOG.md`, a row for 0.3.0 to the table of
versions in `SECURITY.md` and a sentence on the two faults to the rows for
0.2.0 and 0.1.0, a sentence in four places saying 0.3.0 is the third version
meant as a release, and changed the two Known problems entries on 0.1.0 and
0.2.0 to name 0.3.0 as the first version to carry each correction, each worded
so that it was true before and after publishing. No code changed besides the
version.

On the development machine, before the commit, Edwin measured. The output he
pasted held Git's view (five files, 189 lines added, 17 removed) and the end of
SpecCheck's report: 106 requirements, 534 test records, 0 binding gaps, 0
stale, exit code 0. The suites' lines were not in it, so Claude read the result
files the run had written on the development machine, through the link to it:
528 tests passed (28, 27, 47, 61, 44, 205 and 116), none failed and none was
skipped, and the matrix it wrote listed 90 requirements with a passing test.
SpecCheck's counts of assemblies and evidence rows were not in the paste; in
Claude's workspace, on the same files, they were 23 and 18. A fresh clone after
the push matched Claude's copy in all 225 files, and the message too. CI run
#145 passed in 1 minute 40 seconds with no annotations.

Before the commit, the step of `release.yml` that checks a tag against the
version and the changelog was run in Claude's workspace, as written, with
PowerShell 7.4.6: for `v0.3.0` it passed and took 137 lines of notes; for
`v0.2.0` it refused, saying the tag and `Directory.Build.props` disagree.

Claude then asked whether to publish, recommending it. Edwin: "Publish now
(Recommended)". He created the annotated tag `v0.3.0` on the commit, named by
its full hash, with the message "SecretPrinter 0.3.0, the third version meant
as a release.", at 16:55:36Z, pushed it, and approved the signing job.

## The run that published it

Release run #6, read from the run's public page:

| | |
| --- | --- |
| Result | Success, 5 min 27 s: build 1 min 59 s, sign 2 min 5 s, publish 14 s |
| Approval | by `edwest19`, environment `release`, comment "reviewed" |
| Kept | `build-record` 32.4 KB, `signed-release` 35.7 MB, `unsigned-folder` 35.6 MB |
| Coverage matrix | not read; the job's summary needs a sign-in |
| Annotations | none |

The release page, read without signing in: "SecretPrinter 0.3.0", marked
**Latest** and not a pre-release, on commit `064ca9f`, released by
`github-actions` at 17:01Z, with the 0.3.0 entry of `CHANGELOG.md` as its
notes. Its two files, both uploaded at 17:01:03Z:

| File | Size | SHA-256 as GitHub shows it |
| --- | --- | --- |
| `SecretPrinter-0.3.0-win-x64.zip` | 35.8 MB | `74d7bb3f69eb6ca6422e54012e1a65d237fc5d0eb6807d146a279f5c5de46660` |
| `SecretPrinter-0.3.0-win-x64.zip.sha256` | 99 bytes | `b021ede87cf24caa1ab3c33e186a187f9edc02f6be90f104571a0b601b59b6d8` |

## The runtime it carries

Before the version commit, Microsoft's release metadata for .NET, read through
the web that morning, gave 10.0.13 of 2026-10-13 as the latest .NET 10
release, marked as a security release, with SDK 10.0.402 carrying runtime
10.0.13. Its release notes list 44 CVEs by title. `actions/setup-dotnet` v6.0.0,
read at the commit the workflow pins, turns `global.json`'s 10.0.100 with
`rollForward: latestFeature` into "10.0", the newest 10.0 SDK. The SDK the run
used was not read: the job's log needs a sign-in.

**The downloaded copy carries `Microsoft.NETCore.App 10.0.13`,** where 0.1.0
and 0.2.0 carry 10.0.12. Which of the 44 CVEs concern code SecretPrinter runs is
not established; their titles do not say.

The one package the service declares, `System.ServiceProcess.ServiceController`,
stays at 10.0.11, though 10.0.12 and 10.0.13 are on NuGet. In dotnet/runtime,
between the tags `v10.0.11` and `v10.0.13`, its source folder did not change,
nor that of `System.Diagnostics.EventLog`, which it brings, nor any of the
shared files their project files name (25 and 24). That was read in Claude's
workspace from a fetch of the three tags.

## The checks on FIOS-STB-01

All in Edwin's SSH session on FIOS-STB-01, which runs Windows 10.

**The download.** The command went in twice, joined end to end: the first
copy's last command failed with a parameter error, because the second copy's
first characters were joined to its file name, and the second copy then
downloaded both files again over the first. Both `curl.exe` commands of each
copy ended with exit code 0. The zip was 37,581,637 bytes and the `.sha256` 99
bytes, written at 17:04:04Z.

**Check 1, as printed.** `Get-FileHash` gave
`74D7BB3F69EB6CA6422E54012E1A65D237FC5D0EB6807D146A279F5C5DE46660` for both
downloads, and the `.sha256` file held the same 64 characters followed by the
file name. It is also GitHub's digest above.

**The unpacking.** Into a new folder in Edwin's user folder, by a command that
would have refused, and unpacked nothing, had the folder existed; it did not.
The folder was created at 17:06:05Z. No error was reported.

**Check 2, as printed,** on that folder: 216 files and four lines, every one
`Valid`:

| Files | Ours | Signer |
| --- | --- | --- |
| 12 | yes | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
| 185 | no | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | no | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | no | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |

No program file lacked a timestamp. The service's stamp read
`0.3.0+064ca9fc7cf649ff3eed615758274bedb533767f`, the commit the tag names.

**The folder against the zip.** Signatures cover only the program files, and
14 of the 216 are not programs. For each of the zip's 216 files, the SHA-256 of
the file inside the zip and of the same file in the folder: **all 216 were the
same, none was missing, and the folder held no other file.**

## The install

One command, run in the same session, which is elevated, did steps 2 to 5 of
"From a release" with step 4's signature check, as on 2026-10-11:

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
touched.

| Time | What the service logged |
| --- | --- |
| 17:16:42Z | The build of `8663e5c`: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 17:16:48Z | `Service 'SecretPrinter' starting under the service control manager.`; the client interface `Ethernet` (index 19); the printer interface `Wi-Fi` (index 16) |
| 17:16:49Z | The printer's capabilities and connection answered; probing for the advertised names |
| 17:16:50Z | `Probe clear: no other device answered for the advertised names.` |
| 17:16:52Z | `Announced. Answering queries.`; accepting print jobs on the client interface's IPv4 address and on its link-local address |

**The household had no printer for 10 seconds,** from 17:16:42Z to 17:16:52Z.
No warning or error was logged.

## What the build of `8663e5c` did before it was replaced

It had run on FIOS-STB-01 since 2026-10-14 at 20:31:27Z, made there by
`publish-release.ps1` and not signed; its code is that of 0.3.0, but for the
version. Its log was read at 16:35:07Z, for the sentence in the 0.3.0 entry of
`CHANGELOG.md` on where the source code has run:

- **The Windows restarts of 2026-10-14,** at 20:49:12Z and 20:55:00Z, are
  recorded in [the finding of the
  restart](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md).
  Before them, from 20:38:21Z to 20:41:08Z, the printer was withdrawn for 2
  minutes 47 seconds; that part of the log had not been read before.
- **From its last start, at 20:56:38Z on 2026-10-14, to the reading, 19 hours
  38 minutes,** it lost the printer 11 times and found it again each time
  without a restart, offering it again 9 seconds to 6 minutes 24 seconds after
  withdrawing it, 27 minutes 20 seconds in all. Each time it was offered again 3
  seconds after it answered. Ten of the losses came with no line about the
  printer-side adapter.
- **One loss was the printer-side Wi-Fi dropping.** Windows' WLAN-AutoConfig
  log shows the link's security failing at 13:42:54Z ("Dynamic key exchange did
  not succeed within configured time") and the adapter disconnected at
  13:42:55Z ("The network is disconnected by the driver."). The service
  withdrew the printer at 13:44:08Z, 73 seconds later, when its records
  expired, as the Known problems entry on a printer still offered after it
  stops answering says it can be, and could not reopen its printer-side socket,
  the adapter not being up. At 9:50:28 am (13:50:28Z) a script of Edwin West's,
  not part of SecretPrinter, found the Wi-Fi disconnected and ran
  `netsh wlan connect`, which reported "Connection request was completed
  successfully." with exit code 0; Windows logged a "Manual connection with a
  profile" started at 13:50:29.167Z and connected at 13:50:29.308Z. In that
  second the service logged `The printer-side interface is usable again:`,
  reopened its socket, asked the printer at once and was answered
  (`REQ-RES-010`), and it offered the printer again at 13:50:32Z. The Wi-Fi was
  down for 7 minutes 34 seconds; the WLAN-AutoConfig log holds no attempt to
  connect it between the disconnect and the script's.
  The script logged one other reconnect since 2026-10-14 at 4:31 pm: at 5:10:34
  pm that day, after the second boot, which the restart finding records.
- **No lookup failed.** The log holds no `Printer lookup failed` line in that
  time.
- **Connections since its first start:** 79 accepted, 79 relayed with the
  printer's certificate matching the pinned fingerprint, 77 completed; none
  failed and none was refused. The other two are the two that were open at the
  stop of 20:49:12Z on 2026-10-14 and have no line for their end, as the
  restart finding records and `REQ-PXY-009`'s known problem says.

Its stop summary at 17:16:42Z, which counts from its last start, while the
printer was offered (`REQ-LIF-006`):

```
Served 126 quer(ies) of 10413 seen; 10260 were for other services and were ignored; 27 carried every answer already and were not answered (RFC 6762 s7.1); 0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 102 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 113 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
By transport: IPv4 answered 104 of 6562 seen; IPv6 answered 22 of 3851 seen.
```

126, 10,260 and 27 add up to 10,413, and the two transports to 126 and 10,413.
In about 20 hours, 102 of the 126 queries served were answered by unicast
because they asked for it (`REQ-ADV-028`), 113 waited at random first
(`REQ-ADV-029`), none was held back within a second (`REQ-ADV-027`) and none was
refused for its address (`REQ-SEC-017`). Which devices asked is not logged.
What the log holds between 15:51:37Z, its last line at the reading, and the
stop was not read, apart from the stop itself.

## The print

Edwin tapped Print on the iPhone at 1:21 pm, "and it printed a page". The log
from 17:16:42Z, read at 17:23:30Z:

- 6 connections accepted, 6 relayed with the printer's certificate matching the
  pinned fingerprint, 6 completed; none failed and none was refused. All came
  from link-local addresses, and they ended between 17:21:39Z and 17:22:33Z.
- The largest carried 63,508 bytes to the printer and 8,771 back in 78.55
  seconds; the other five carried between 662 and 13,366 bytes to the printer.
  Which of them carried the page is not in the log.

## What Claude predicted, and what it did not

Held: the release metadata's runtime, 10.0.13, as what the build would carry;
the CI runs with no annotations; the tag check; the download's exit codes;
check 1, the published digest and the `.sha256` file the same; 216 files;
check 2's four lines, all `Valid`, 12 under the expected name; no file without a
timestamp; the stamp; the folder the same as the zip, file by file; the
install's lines, from the old build's stop to `Accepting print jobs`; no warning
or error; the counts of the print's connections being equal, none failed. The
zip's size, the folder's creation time and the numbers in the stop summary were
said in advance to be unknown.

Not predicted:

- **In the reading of the build of `8663e5c`:** the withdrawal at 20:38:21Z on
  2026-10-14; a line `usable again` at 21:10:38Z that day, which Claude
  predicted although the reading's filter does not select the line the startup
  wait logs when it ends; the script's reconnect that day at 5:10:34 pm, given
  as 5:10:29 pm, when that run of the script began. Windows' boot time read
  20:55:31Z, where the record of 2026-10-14 says 20:55:33Z; why the two differ
  is not established.
- **The print:** Claude said about ten connections, and a connection of
  hundreds of kilobytes for the page. There were six, and the largest carried
  63,508 bytes.
- **The download's command went in twice.** It changed nothing: both downloads
  had the same hash.
- **In the reading of the Wi-Fi's drop at 9:50 am,** made to check the sentence
  on it in the 0.3.0 entry before this finding was written: Claude predicted
  the script's run at 9:40 am would find the printer's port not answering, but
  9:40 am (13:40Z) was before the drop and it answered; and gave the disconnect
  as about 13:44Z, when the service withdrew the printer, where Windows logged
  it at 13:42:55Z. The script's `netsh wlan connect`, its exit code 0 and
  Windows' "Manual connection with a profile" at 13:50:29Z were as predicted.

## What this does not show

- **Updating from one release to the next,** by the document's steps. The
  folder 0.3.0 replaced was the unsigned build of the same source code, not
  0.2.0.
- **0.3.0 on Windows 11, or on a machine with no .NET.** FIOS-STB-01 has .NET
  SDKs installed; the release carries its own runtime.
- **A Windows shutdown or restart with 0.3.0 running,** or a start of 0.3.0 at
  boot. `REQ-LIF-009` was run at a real restart in the build of `8663e5c`.
- **Which SDK the workflow used,** and the coverage line of the job's summary.
  Neither was read.
- **Which of the 44 CVEs of .NET 10.0.13 concern code SecretPrinter runs.**
- **That the service runs on 10.0.13 as it did on 10.0.12** beyond this
  start-up and this print: the build of `8663e5c` carried 10.0.12.

## How it was read

Commands given to Edwin, each read-only except the install, run by him in an
SSH session on FIOS-STB-01 or in PowerShell on the development machine, and
pasted. GitHub's run and release pages were read without signing in, through a
tool that fetches a page and returns a model's reading of it. Microsoft's
release metadata and release notes, NuGet's list of the package's versions and
GitHub's changelog were read the same way. The development machine's result
files were read through the link to it. Who reconnected the Wi-Fi at 9:50 am
was read from the script's own log and Windows' WLAN-AutoConfig operational
log, with the network's name and hardware addresses masked. The pasted logs hold the household's
addresses, the printer's host name and the pinned fingerprint; none of them is
repeated here.
