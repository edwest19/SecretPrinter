# The second release was published and installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-11. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-11 Edwin West chose the version number
0.2.0, pushed the tag `v0.2.0` on commit `5b846ba` and approved the signing
job, and the release workflow published SecretPrinter 0.2.0, marked Latest. On
FIOS-STB-01 a copy downloaded with `curl.exe` passed both checks of
[`operating.md`](../operating.md), carried .NET 10.0.12 and the stamp of the
tagged commit, and was installed in place of the unsigned build that had run
there overnight, by the "From a release" update steps with the signature
check. The household had no printer for 10 seconds. An iPhone printed a page
through it. Updating from one release to the next is still not shown: what
0.2.0 replaced was a build, not 0.1.0. No code changed.**

*(Status note, 2026-10-12, by Claude, Claude Opus 5.5: the start over
(`REQ-RES-010`) has now run in this release. FIOS-STB-01's log was read at
2026-10-12 20:12:21Z, 23 hours 32 minutes after 0.2.0 started, and nothing was
restarted or changed. In that time the service lost the printer 11 times and
found it again each time without a restart; it was not offered for 3 hours 3
minutes in all. Four of the losses ended only after the printer-side Wi-Fi had
gone down and been reconnected. Each reconnect was made by a script of Edwin
West's, not part of SecretPrinter; Windows reported the Wi-Fi connected at
00:30:29Z, 10:40:29Z, 13:10:29Z and 18:50:29Z on 2026-10-12. The line `The
printer-side interface is usable again:` followed 2 to 5 seconds later, the
printer was held reachable again in that second or the next, and it was
offered again 5 to 8 seconds after the reconnect. Windows did not reconnect
the Wi-Fi by itself in any of the four. Its log shows "Wireless security
failed" and a disconnect before two of the reconnects, and before the other two
"Wireless security stopped" and nothing more until the script connected it. In
the third, by the code, the printer-side socket was reopened at 13:10:29Z for a
question the schedule put, in the second Windows reported the Wi-Fi connected,
and the start over's line came five seconds later, at 13:10:34Z; which of the
two questions the printer answered is not in the log. The first loss lasted 2
hours 14 minutes 30 seconds, from 22:16:03Z on 2026-10-11. The script found the
printer's port 631 not answering from 22:21:15Z and answering again at
00:00:45Z, and at each of its tests after that. Windows logged "Wireless
security stopped" at 00:21:42Z, the service found the adapter not up at
00:25:27Z, and after the reconnect it found the printer at 00:30:33Z, one
second after the start over. So for half an hour the printer answered on its
port and was not offered, as [`operating.md`](../operating.md) says can happen
after a long loss; when the schedule would have asked next had the Wi-Fi stayed
up is not worked out. The other seven losses ended at a question the schedule
put, 23 seconds to 9 minutes 18 seconds after they began. Whether the start
over made any loss longer or shorter is not worked out. All 93 connections
since the start completed; none failed and none was refused. Apart from the
page of 2026-10-11, none carried more than 9,988 bytes to the printer.)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does the printer's host name.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
the commands run on FIOS-STB-01, or from GitHub's pages. Times in pm are US
Eastern. Everything here happened on 2026-10-11.

## The version, the commit and the tag

Claude asked Edwin for the version number, recommending 0.2.0:
`CHANGELOG.md` says versions follow Semantic Versioning, and its FAQ says of
versions below 1.0.0 to "increment the minor version for each subsequent
release". Since 0.1.0 the service had gained two behaviours, `REQ-ADV-026` and
`REQ-RES-010`. Edwin chose "0.2.0 (Recommended)".

The commit `5b846ba027ab96372bde70295009334b37b54405`, on `7727973`, set
`<Version>` to 0.2.0 in `Directory.Build.props`, added the 0.2.0 entry to
`CHANGELOG.md`, a row for 0.2.0 to the table of versions in `SECURITY.md`, and a
sentence in four places saying 0.2.0 is the second version meant as a release,
worded so that it is true before and after publishing. No code changed besides
the version. On the development machine, before the commit, Edwin measured 452
tests passing and SpecCheck at 101 requirements, 23 assemblies, 18 evidence
rows, 458 test records, 85 requirements with a passing test, 0 binding gaps,
exit code 0. A fresh clone after the push matched Claude's copy in all 207
files, and the message too. CI run #132 passed in 1 minute 9 seconds. In
Claude's workspace, a simulation of the workflow's check of the tag found one
heading for 0.2.0 and 72 lines of notes under it.

Claude then asked whether to publish, recommending it. Edwin: "Publish now
(Recommended)". He created the annotated tag `v0.2.0` on the commit, named by
its full hash, with the message "SecretPrinter 0.2.0, the second version meant
as a release.", at 20:25:50Z, and pushed it.

## The run that published it

Release run #5, read from Edwin's screenshot and the run's public page:

| | |
| --- | --- |
| Result | Success, 4 min 30 s: build 1 min 37 s, sign 1 min 47 s, publish 13 s |
| Approval | by `edwest19`, environment `release`, comment "reviewed" |
| Kept | `build-record` 29.1 KB, `signed-release` 35.7 MB, `unsigned-folder` 35.6 MB |
| Coverage matrix | "84 of 101 requirements fully covered" |
| Annotations | none |

The release page, read without signing in: "SecretPrinter 0.2.0", marked
**Latest** and not a pre-release, on commit `5b846ba`, released by
`github-actions` at 20:30Z, with the 0.2.0 entry of `CHANGELOG.md` as its notes.
Its two files, both uploaded at 20:30:20Z:

| File | Size | SHA-256 as GitHub shows it |
| --- | --- | --- |
| `SecretPrinter-0.2.0-win-x64.zip` | 35.8 MB | `a237646a02776288efd01fee865b15223493d677efc3644b63451b6aaa77b176` |
| `SecretPrinter-0.2.0-win-x64.zip.sha256` | 99 bytes | `f5301a564cc974f94b2778f0890d31afaf21c9d72d231a631bb569a6792152ae` |

## The runtime it carries

Before the tag, Microsoft's release metadata for .NET, read through the web,
gave 10.0.12 of 2026-09-08 as the latest .NET 10 release and 10.0.401 as the
latest SDK. The workflow installs an SDK by `actions/setup-dotnet` from
`global.json`, which asks for 10.0.100 or a later feature band. That action's
documentation, at the commit the workflow pins, says that for `latestFeature`
"the action installs the appropriate SDK version". The SDK the run used was not
read: the job's log needs a sign-in.

The downloaded copy carries `Microsoft.NETCore.App 10.0.12`, the same as 0.1.0.
Microsoft's support policy says updates come on "Patch Tuesday" (second Tuesday
of each month), "however there is no guarantee that there will be a .NET
release on any given Patch Tuesday". The next is 2026-10-13.

## The checks on FIOS-STB-01

All in Edwin's SSH session on FIOS-STB-01, which runs Windows 10.

**The download.** The two `curl.exe` commands under "From a release", step 1,
with the version filled in, each ended with exit code 0. Neither file was
there before. The zip was 37,561,178 bytes and the `.sha256` 99 bytes, written
at 20:32:17Z and 20:32:18Z.

**Check 1, as printed.** `Get-FileHash` gave
`A237646A02776288EFD01FEE865B15223493D677EFC3644B63451B6AAA77B176`, and the
`.sha256` file held the same 64 characters followed by the file name. It is
also GitHub's digest above.

**The unpacking, and what it found.** The document's `Expand-Archive` command
unpacked the zip into a new folder in Edwin's user folder. The output Edwin
sent begins partway through a list of errors from `Expand-Archive`, for the
files from `System.ValueTuple.dll` to `WindowsBase.dll`, each "Failed to create
file … already exists. Use the -Force parameter if you want to overwrite". So when that `Expand-Archive` ran the folder already held those
files, and it left them as they were. The start of the output, which would have
shown whether the folder existed before, was not in what was sent. The folder
was created at 20:33:36Z, 79 seconds after the download. The likeliest account
is that the command ran twice and the second run found the first run's files;
that is not established. Claude's command printed whether the folder existed
before unpacking, but went on regardless. It should have refused.

Signatures cover only the program files, and 14 of the 216 files are not
programs. So, before anything was installed, the folder was compared with the
zip: for each of the zip's 216 files, the SHA-256 of the file inside the zip
and of the same file in the folder. **All 216 were the same, none was missing,
and the folder held no other file.** The zip compared against was the one
check 1 had proved.

**Check 2, as printed,** on that folder: 216 files and four lines, every one
`Valid`:

| Files | Ours | Signer |
| --- | --- | --- |
| 12 | yes | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
| 185 | no | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | no | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | no | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |

No program file lacked a timestamp. The service's stamp read
`0.2.0+5b846ba027ab96372bde70295009334b37b54405`, the commit the tag names.

## The install

One command, run in the same session, which is elevated, did steps 2 to 5 of
"From a release" with step 4's signature check:

- It first tested TCP port 631 on the printer, and would have stopped there,
  touching nothing, if the printer had not answered. It answered.
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
| 20:39:54Z | The overnight build: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 20:40:00Z | `Service 'SecretPrinter' starting under the service control manager.`; the printer interface `Wi-Fi -> 192.168.12.136 (index 27)` |
| 20:40:01Z | Capabilities read from the printer; probing for the advertised names |
| 20:40:02Z | `Probe clear: no other device answered for the advertised names.` |
| 20:40:04Z | `Announced. Answering queries.`; accepting print jobs on 192.168.1.161:631 and on the client interface's link-local address |

**The household had no printer for 10 seconds,** from 20:39:54Z to 20:40:04Z.
No warning or error was logged.

## What the overnight build said when it stopped

The build of `5db9063` had run since 2026-10-10 21:52:58Z, 22 hours 46 minutes
56 seconds
([finding](2026-10-11-the-start-over-ran-overnight-on-fios-stb-01.md)). Its stop
summary:

```
Served 163 quer(ies) of 9365 seen; 9138 were for other services and were ignored; 64 carried every answer already and were not answered (RFC 6762 s7.1).
By transport: IPv4 answered 86 of 5881 seen; IPv6 answered 77 of 3484 seen.
```

163, 9,138 and 64 add up to 9,365, and the two transports to 163 and 9,365.
It is the first count from FIOS-STB-01 of questions that known-answer
suppression (`REQ-ADV-026`) left unanswered: 64 in nearly 23 hours. Which
questions they were is not logged. What the service logged between 19:24:22Z,
when the overnight run was read, and the stop was not read, apart from the stop
itself.

## The print

Edwin tapped Print on the iPhone at 4:43 pm, and the page "printed
immediately". He also opened the printer's information screen on the iPhone
twice. The log from 20:40:00Z, read at 20:45:46Z:

- 13 connections accepted, 13 relayed with the printer's certificate matching
  the pinned fingerprint, 13 completed; none failed and none was refused.
- **The page:** one connection carried 1,059,949 bytes to the printer and 3,277
  back in 11.11 seconds, ending at 20:43:45Z.
- The other 12 carried between 603 and 9,988 bytes to the printer. Which of
  them were the information screens is not established.

## What Claude predicted, and what it did not

Held: the download's exit codes; check 1, the published digest and the
`.sha256` file the same; 216 files; check 2's four lines, all `Valid`, 12
under the expected name; no file without a timestamp; runtime 10.0.12; the
stamp; the folder the same as the zip, file by file; the install's lines, from
the soak build's stop summary to `Accepting print jobs`; no warning or error;
the counts of the print's connections. The zip's size, the folder's creation
time and the numbers in the stop summary were said in advance to be unknown.

Not predicted:

- **The `Expand-Archive` errors.** The prediction was `exists before: False`.
- **The outage.** Claude said about half a minute, allowing for the signature
  check inside it. It was 10 seconds.

## What this does not show

- **Updating from one release to the next,** by the document's steps. The
  folder 0.2.0 replaced was the unsigned build of the same source code, not
  0.1.0.
- **0.2.0 on Windows 11, or on a machine with no .NET.** FIOS-STB-01 has .NET
  SDKs installed; the release carries its own runtime.
- **The start over in this build.** The printer had not been lost between the
  start and the reading at 20:45:46Z. *(Status note, 2026-10-12, by Claude,
  Claude Opus 5.5: shown since, four times; see the status note at the top.)*
- **A start of 0.2.0 at boot.**
- **Which SDK the workflow used,** or whether "84 of 101 requirements fully
  covered" counts the same thing as the development machine's "85 requirements
  with a passing test". Neither was read.
- **How the files came to be in the folder** before the `Expand-Archive` that
  reported them. That they were the zip's own was measured.

## What changed

- This finding.
- `CHANGELOG.md`: at the top, "`0.2.0` is the second version meant as a
  release." made exact; under 0.2.0, "What happened", which is not in the
  release notes GitHub shows.
- `SECURITY.md`: the first sentence of "Supported versions" and the row for
  0.2.0 made exact.
- `README.md`: section 12, the sentence on 0.2.0 made exact, the paragraph on
  the release workflow given its fifth run, and the paragraph on FIOS-STB-01
  given this install; open question 10, which build FIOS-STB-01 runs; the
  known problem on RFC 6762, and the `REQ-ADV-026` row, given the count from
  FIOS-STB-01; the Known problems section's opening note.
- `docs/operating.md`: "Where the program comes from", the sentence on 0.2.0
  made exact and this install added; "Checking a release before you install
  it", this run of both checks, and what `Expand-Archive` does with a folder
  that already holds the files; "Updating an installed service", this update
  and the stamp 0.2.0 printed.
- `docs/verification.md`: this finding added to the `REQ-DIST-004`,
  `REQ-DIST-005` and `REQ-DIST-006` rows, each with a note.
- A status note on
  [the finding of the overnight run](2026-10-11-the-start-over-ran-overnight-on-fios-stb-01.md).
