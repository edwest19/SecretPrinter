# The fifth release was published and installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-25. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-25 Edwin West chose to release 0.5.0,
pushed the tag `v0.5.0` on commit `1f1f1ee` and approved the signing job, and
the release workflow published SecretPrinter 0.5.0, marked Latest. It is the
first release to take the printer's own records, heard on the printer-side
network without being asked, as an answer while the service holds the printer
unreachable (README `REQ-RES-011`), and the first in which a lookup does not
take a goodbye record as its answer. The section "Built with" named .NET SDK
10.0.403 and the runtime Microsoft.NETCore.App 10.0.14: Microsoft had released
10.0.14 that day, as an out-of-band security update. The release notes, taken
from `CHANGELOG.md`, say Microsoft's latest .NET 10 release that day was still
10.0.13; that sentence was untrue when Claude wrote it, from one reading of one
page, and a correction was added to the published notes after publishing. On
FIOS-STB-01 a copy downloaded with `curl.exe` passed both checks of
[`operating.md`](../operating.md), carried the runtime 10.0.14 and the stamp of
the tagged commit, was the same as the zip file by file, and was installed by
the "From a release" update steps with the signature check, in place of an
unsigned build of `5a291c4` made there on 2026-10-22. The household had no
printer for 10 seconds. The first line the new service logged was its version,
and an iPhone printed a page through it. No code changed after the tag.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name or the printer's certificate fingerprint.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
the commands run on FIOS-STB-01, or from GitHub's pages. Times in am or pm are
US Eastern, UTC-4; Edwin's are by his iPhone's clock, to the minute, and that
clock was not compared with FIOS-STB-01's. Everything here happened on
2026-10-25.

## What went into it

Nine commits came after the one `v0.4.0` names, up to and including the one
`v0.5.0` names. One changed what the program does:

- `cd89eac`, of 2026-10-22: `REQ-RES-011`, and lookups no longer taking a
  record with a TTL of 0 as their answer
  ([the finding](2026-10-22-the-service-hears-the-printer-announce-itself.md)).

`1f1f1ee` set the version. The other seven changed documents: `1504b7e`,
which also changed the header comment of `release.yml` and no step, `ab6cc59`,
`43dde3b`, `16691f4`, `5a291c4`, `3d654fc` and `2ab8e87`.

`cd89eac` had run on a network before the release, in a build of `5a291c4` made
from source on FIOS-STB-01, not signed and carrying .NET 10.0.12, from
2026-10-22 to this install
([the finding of that run](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md)).
Its first three days there, read today before the version was set, are in that
finding's status note of 2026-10-25, which `2ab8e87` added: no restart; 33
losses of the printer, each found again without a restart; the printer heard
without being asked once, in the test of 2026-10-22, and in none of the 31
losses after it.

## The decisions

Claude asked what FIOS-STB-01 should run, with three options: "Release 0.5.0
(Recommended)", "Keep this build for now" and "Go back to 0.4.0". Edwin chose
"Release 0.5.0 (Recommended)". Before the version was set, Claude read the
build's first three days, so that the entry could say where the change had
run, and so that nothing would be released over a problem the log showed; the
log showed none.

After the version commit, Claude asked whether to publish, recommending it.
Edwin: "Publish now (Recommended)". After the checks on FIOS-STB-01, Claude
asked whether to install, recommending it. Edwin: "Install now
(Recommended)". After the run, Claude asked whether the published notes should
carry a correction of its wrong sentence (below). Edwin: "Add a correction
(Recommended)".

## The two commits before the tag

**`2ab8e87`, the three days.** Three status notes on the finding of the run of
2026-10-22 and a note in `docs/operating.md`. On the development machine
before the commit, Edwin measured: Git's view (two files, 64 lines added, 1
removed); 571 tests passing (28, 27, 49, 62, 74, 205 and 126), counted by
Claude from the seven results files that run wrote, every outcome `PASS`,
because the pasted output began below the tests; and SpecCheck 108
requirements, 578 test records, 91 covered by code and 17 by evidence, 105
binding and all covered, 0 binding gaps, 0 marked not run, 0 stale, `PASS`,
exit code 0. CI run #165 passed in 1 minute 37 seconds. A fresh clone after the
push matched Claude's copy in all 238 files, and the message too.

**`1f1f1ee`, the version.** `<Version>` set to 0.5.0 in
`Directory.Build.props`; the 0.5.0 entry in `CHANGELOG.md` and a sentence at
its top; a row for 0.5.0 in the table of versions in `SECURITY.md`, with 0.5.0
in its first sentence and `v0.5.0` in its paragraph on tags; a sentence in
README section 12 and in `docs/operating.md` saying 0.5.0 is the fifth version
meant as a release; and, in README and `docs/operating.md`, the three places
that said no release carries `REQ-RES-011` made to say that 0.5.0 is the first
to carry it. Each sentence on 0.5.0 was worded to be true before and after
publishing. No sentence was added to the rows of `SECURITY.md` for earlier
versions, and a note there says why. No code changed besides the version.

On the development machine, before the commit, Edwin measured: Git's view (five
files, 169 lines added, 18 removed), a build with 0 warnings and 0 errors, 571
tests passing (28, 27, 49, 62, 74, 205 and 126, none failed or skipped), and
SpecCheck 108 requirements, 23 assemblies, 18 evidence rows, 578 test records,
92 requirements with a passing test, 91 covered by code and 17 by evidence,
105 binding and all covered, 0 binding gaps, no orphaned or malformed markers,
duplicate IDs or orphaned evidence, 0 marked not run, 0 stale, `PASS`, exit
code 0. The matrix that run wrote said 91 of 108 fully covered. CI run #166
passed in 1 minute 37 seconds with no annotations. A fresh clone after the
push matched Claude's copy in all 238 files, and the message too.

Before the commit, the step of `release.yml` that checks a tag against the
version and the changelog was run in Claude's workspace, as written, with
PowerShell 7.4.6: for `v0.5.0` it passed and gave 110 lines of notes, 117 after
the step "Name the SDK and the runtime" was run after it on a made-up folder;
for `v0.4.0` it refused, saying the tag and `Directory.Build.props` disagree.

Edwin created the annotated tag `v0.5.0` on the commit, named by its full hash,
with the message "SecretPrinter 0.5.0, the fifth version meant as a release.",
at 21:58:57Z, pushed it, and printed the commit it names:
`1f1f1ee984f8881528518a140d5e400d75f21ab2`. The command was first run in
Claude's workspace, in a scratch clone, with nothing pushed. He approved the
signing job when it waited.

## The run that published it

Release run #8, read from the run's page in the desktop app's browser pane:

| | |
| --- | --- |
| Result | Success, 4 min 50 s: build 2 min 0 s, sign 1 min 39 s, publish 15 s |
| Approval | by `edwest19`, environment `release`, comment "reviewed" |
| Kept | `build-record` 32.9 KB, `signed-release` 35.7 MB, `unsigned-folder` 35.6 MB |
| Annotations | one notice, "Built with": ".NET SDK 10.0.403. The folder carries Microsoft.NETCore.App 10.0.14." |

The release page: 0.5.0, marked **Latest** and not a pre-release, on commit
`1f1f1ee`, released by `github-actions` at 22:03:49Z, with the 0.5.0 entry of
`CHANGELOG.md` as its notes, followed by the section "Built with": ".NET SDK
10.0.403, as `dotnet --version` named it on the build machine." and "The .NET
runtime in the folder: Microsoft.NETCore.App 10.0.14, as
`SecretPrinter.Service.runtimeconfig.json` names it." Its two files, both
uploaded at 22:03:47Z:

| File | Size | SHA-256 as GitHub shows it |
| --- | --- | --- |
| `SecretPrinter-0.5.0-win-x64.zip` | 35.8 MB | `c16bd249362e0ed71efecab380369c4604afc81df8040296a3da9c5d1fd7e568` |
| `SecretPrinter-0.5.0-win-x64.zip.sha256` | 99 bytes | `a1e1b40493cb9bcafe525ed19e90da5ac73fe3e62b8b67154481af891d0cade3` |

A copy of both, downloaded in Claude's workspace with `curl`, gave the same two
digests; the zip held 216 files, its `SecretPrinter.Service.runtimeconfig.json`
named `Microsoft.NETCore.App 10.0.14`, and its `SecretPrinter.Service.dll`
carried the stamp `0.5.0+1f1f1ee984f8881528518a140d5e400d75f21ab2`. The
signatures cannot be read there.

## The runtime it carries, and a sentence that was untrue

**What the entry said.** Microsoft's download page for .NET 10, read through a
fetch tool shortly after 3:24 pm, gave 10.0.13, of 2026-10-13, as the latest
release, with SDK 10.0.402. The 0.5.0 entry was written from that one reading:
"Microsoft's latest .NET 10 release on 2026-10-25 was still 10.0.13, of
2026-10-13, which `0.4.0` carries." The message of `1f1f1ee` says the same. It
was never checked against a second source.

**What was so.** Microsoft's announcement on GitHub, dotnet/announcements
issue 496, opened at 19:06:41Z on 2026-10-25, says that on that day Microsoft
released out-of-band security updates, among them .NET 10.0.14 with SDKs
10.0.114 and 10.0.403. It lists four advisories: CVE-2026-111497, in the
Brotli decoder; CVE-2026-114148, in the file ownership of some Linux
archives; CVE-2026-70197, in `System.Net.Sockets` on Windows; and
CVE-2026-102870, in packages of the Blazor WebAssembly debugger. It says the
updates of 2026-10-13 did not fully address the last two. The page of the
release on dotnet/dotnet says it was released "25 Oct 18:19", in a time zone
the page does not name. So 10.0.14 had been announced before the download page
was read, and the sentence was untrue when it was written. Why the page gave
10.0.13, whether it had not been updated or the fetch tool returned an older
copy, is not shown. Claude relied on one reading of one page; that is Claude's
mistake. The section "Built with", which the workflow writes from the build
itself, gave the truth in the same notes.

**What was done about it.** Edwin chose to correct the published notes. Claude
gave him one line, which he added through Edit release on GitHub at the end of
the entry's bullet on the runtime, leaving the wrong sentence where it was:
"(Correction, 2026-10-25, by Claude, Claude Opus 5.5, added after publishing:
the sentence above that begins "Microsoft's latest .NET 10 release" is wrong.
...)", in italics, naming 10.0.14 and this finding. Read back in the browser
pane: the line was there, after "as in `0.4.0`."; the release was still marked
Latest and not a pre-release; and its two files had the same digests as before.
Editing the notes changed neither the tag, nor the files, nor their signatures.
In `CHANGELOG.md`, the commit that adds this finding corrects the sentence,
with a note giving what it said.

**Which runtime each release carries.** `0.5.0` carries 10.0.14; `0.3.0` and
`0.4.0` carry 10.0.13; `0.1.0` and `0.2.0` carry 10.0.12, as their changelog
entries say.

**What is not known: whether CVE-2026-70197 concerns code SecretPrinter
runs.** Its advisory, dotnet/announcements issue 493, describes a fault on
Windows when one `SocketAsyncEventArgs` is reused across an IPv4 and an IPv6
datagram socket, reached through `ReceiveFromAsync`. SecretPrinter's own code
creates no `SocketAsyncEventArgs`: `MdnsSocket` receives with
`ReceiveMessageFromAsync`, on one socket for IPv4 and another for IPv6, and the
probe uses `ReceiveFromAsync` on one socket. Whether the runtime's own code
under those calls is what the advisory describes is not worked out here. None
of the other three advisories was examined against SecretPrinter either.
*(Status note, 2026-10-28, by Claude, Claude Opus 5.5: worked out for all four.
By what Microsoft says each fault needs, SecretPrinter reaches none of them.
For this one, the runtime keeps the object behind those calls with the socket
it is called on, so one is never handed from the IPv4 socket to the IPv6 one;
read in the runtime shipped in releases 0.1.0, 0.4.0 and 0.5.0. See
[the finding](2026-10-28-the-four-advisories-of-2026-10-25-were-read-against-secretprinter.md).)*

## The checks on FIOS-STB-01

All in Edwin's SSH session on FIOS-STB-01, which runs Windows 10, by one
command, read-only but for the two files it downloaded and the folder it made.
It would have stopped, downloading and unpacking nothing, had either file or
the folder already existed. Claude ran it first in its workspace against the
real zip, with stand-ins for the parts that run only on Windows.

**The download.** Both `curl.exe` commands ended with exit code 0. The zip was
37,588,409 bytes.

**Check 1, as printed.** `Get-FileHash` gave
`C16BD249362E0ED71EFECAB380369C4604AFC81DF8040296A3DA9C5D1FD7E568`, and the
`.sha256` file held the same 64 characters followed by the file name. It is
also GitHub's digest above.

**The unpacking.** Into a new folder, `sp-0.5.0`, in Edwin's user folder.

**Check 2, as printed,** on that folder: four lines, every one `Valid`:

| Files | Ours | Signer |
| --- | --- | --- |
| 185 | no | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | no | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | no | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 12 | yes | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |

No program file lacked a timestamp. The folder held 216 files, named its
runtime as `Microsoft.NETCore.App 10.0.14`, and the service's stamp read
`0.5.0+1f1f1ee984f8881528518a140d5e400d75f21ab2`, the commit the tag names.

**The folder against the zip.** For each of the zip's 216 files, the SHA-256 of
the file inside the zip and of the same file in the folder: **all 216 were the
same, none was missing, and the folder held no other file.**

## The install

One command, run in the same session, which is elevated, did steps 2 to 5 of
"From a release" with step 4's signature check:

- It first checked that the session was elevated, that the printer answered
  on TCP port 631, and that the new folder's stamp was the one above, and
  would have touched nothing had any failed. All three held.
- Step 2: stopped the service, removed the program folder, and confirmed it
  gone.
- Step 3: copied the new folder in: 216 files.
- Step 4: ran the signature check on the installed folder, which gave the same
  four lines, and read the stamp. It would have left the service stopped
  unless the stamp was the one above, every program file was `Valid`, and
  SecretPrinter's twelve were signed under the name above. All three held.
- Step 5: started the service and, 20 seconds later, read the log since the
  stop, printing the lines that are not the indented listings of the
  configuration and the advertisement, with addresses masked; it counted 46
  such listing lines among 72.

The service's registration, configuration, log and firewall rules were not
touched. **What it replaced was the build of `5a291c4`** installed there on
2026-10-22, which was not signed and carried .NET 10.0.12.

| Time | What the service logged |
| --- | --- |
| 22:29:03Z | The build of `5a291c4`: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 22:29:09Z | **`SecretPrinter.Service version 0.5.0+1f1f1ee984f8881528518a140d5e400d75f21ab2`**, then `starting under the service control manager`, and the resolver's socket on the Wi-Fi |
| 22:29:10Z | The responder's sockets on the Ethernet; the printer's capabilities and connection answered; probing for the advertised names |
| 22:29:11Z | `Probe clear: no other device answered for the advertised names.` |
| 22:29:13Z | `Announced. Answering queries.`, then accepting print jobs on the client interface's IPv4 address and on its link-local address |

**The household had no printer for 10 seconds,** from 22:29:03Z to 22:29:13Z.
No warning or error was logged.

## What the build said when it stopped

Its stop summary at 22:29:03Z counts from its only start, at 21:33:42Z on
2026-10-22, 3 days 55 minutes 21 seconds before:

```
Served 431 quer(ies) of 38727 seen; 38208 were for other services and were ignored; 88 carried every answer already and were not answered (RFC 6762 s7.1); 0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 387 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 414 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
By transport: IPv4 answered 409 of 27078 seen; IPv6 answered 22 of 11649 seen.
```

431, 38,208, 88, 0 and 0 add up to 38,727, and the two transports to 431 and
38,727.

## The print

Edwin printed a page from the iPhone at 6:38 pm by its clock: "yes it printed
a page". The log from 22:29:13Z, read at 22:39:43Z:

- 5 connections accepted, the first at 22:37:45Z, all located from the
  resolver's cache, and all 5 relayed with TLS 1.2 and the printer's
  certificate matching the pinned fingerprint. All came from one IPv6
  link-local address, and none over IPv4.
- 4 had completed when the log was read, between 22:38:34Z and 22:39:00Z,
  33.71 to 56.47 seconds after they began; the fifth was still open. None
  failed and none was refused.
- One carried 1,064,462 bytes to the printer and 5,577 back in 56.47 seconds;
  the other three that had completed carried 2,512 to 3,416 bytes to the
  printer. On 2026-10-20 one connection of 0.4.0's print carried 1,064,475.
  Which connection carried this page is not in the log.
- The first connection began before 6:38 pm by Edwin's clock; the clocks were
  not compared, and what opened it is not in the log.
- No warning or error was logged from 0.5.0's start to the reading.

## What Claude predicted, and what it did not

Held: the CI runs; the tag check; the commit the tag names; the download's exit
codes; check 1, the published digest and the `.sha256` file the same; check
2, four lines, all `Valid`, 12 under the expected name and 190 by Microsoft;
no file without a timestamp; 216 files; the stamp; the folder the same as the
zip, file by file; the install's lines, from the build's stop to `Accepting
print jobs`, with the version line first; 46 listing lines; no printer for 5 to
10 seconds, at 10; no warning or error; for the print, 5 to 14 connections, all
link-local, from the cache, with TLS 1.2 and the pin matched, none failed or
refused, and one of 1,060,000 to 1,075,000 bytes. Said in advance to be
unknown: the zip's size, whether a connection would still be open at the
reading, and the order of check 2's lines.

Not held:

- **The runtime and the SDK.** Claude predicted the run would name SDK
  10.0.402 and runtime 10.0.13, from the same one reading that produced the
  wrong sentence above. It named 10.0.403 and 10.0.14.
- **"One line that is not a `Job:` line"** after the print: there were three.
  The read began at 22:29:13Z, the second of the install's last lines, and so
  printed `Announced` and the IPv4 `Accepting` line again, beside the
  link-local `Accepting` line that the paste of the install's read had cut off.
- **Earlier in the session,** for the read of the build's three days: Claude
  said job failures would come only around a loss, and one came 1 to 2 minutes
  after the printer was offered again; it said the read would print 30 to 50
  lines, and it printed 117, 61 of them lines logged once, at the build's
  start; and its masking left the letters of the certificate fingerprint, and
  the text of the printer's own TXT note, in two lines of what was pasted.
  Neither is in this repository.
- **A command that did not run.** For the measurement before `2ab8e87`, Claude
  first gave `.\run-tests.ps1` and `.\run-speccheck.ps1`, which Windows'
  execution policy on the development machine refused, instead of the form the
  README gives for this repository, `powershell -ExecutionPolicy Bypass -File`.

## Sentences the release made exact

Sentences written in `1f1f1ee` to be true before and after publishing, which
said 0.5.0 is the fifth version meant as a release, or the first meant as one
to carry `REQ-RES-011`, are made exact by the commit that adds this finding,
each with a note giving what it said, as for each release before: in
`CHANGELOG.md`, `README.md` (section 12, the note on `REQ-RES-011` and the
Known problems entry on the printer-side network), `SECURITY.md` and
`docs/operating.md`.

## What this does not show

- **0.5.0 over time.** One start and one print, within minutes of the install.
- **0.5.0 hearing the printer.** In this run the printer was not lost; what the
  release does when the printer announces itself was shown only in the build of
  `5a291c4`, once, on 2026-10-22.
- **A Windows shutdown or restart with 0.5.0 running,** or a start of 0.5.0 at
  boot.
- **0.5.0 on Windows 11, or on a machine with no .NET.** FIOS-STB-01 has .NET
  SDKs installed; the release carries its own runtime.
- **A download with a browser.** Both checks were run on a copy `curl.exe`
  fetched.
- **Whether any of the four advisories of 2026-10-25 concerns code
  SecretPrinter runs,** above. *(Status note, 2026-10-28, by Claude, Claude
  Opus 5.5: worked out; see the status note above and
  [the finding](2026-10-28-the-four-advisories-of-2026-10-25-were-read-against-secretprinter.md).)*

## How it was read

Commands given to Edwin, each read-only except the download and the install,
run by him in an SSH session on FIOS-STB-01 or in PowerShell on the development
machine, and pasted or attached. Claude checked each with PowerShell's parser
before sending it. GitHub's run and release pages, and Microsoft's
announcements on GitHub, were read through the browser pane of the Claude
desktop app on the development machine; nothing was clicked or changed there.
Microsoft's download page was read through a fetch tool. The commands masked
link-local and other IPv6 addresses, IPv4 link-local addresses, the printer's
host name and UUIDs; the machine's and the printer's addresses on both networks
were in what was pasted and are not given here.
The arithmetic was done in Claude's workspace.
