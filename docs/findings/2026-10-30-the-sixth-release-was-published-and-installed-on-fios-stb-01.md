# The sixth release was published and installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-30. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-30 Edwin West chose to release 0.6.0,
pushed the tag `v0.6.0` on commit `49e8602` and approved the signing job, and
the release workflow published SecretPrinter 0.6.0, marked Latest. It is the
first release in which a lookup of the printer, and the check for a conflict
over the service's own names, take only a response sent to the mDNS group from
UDP port 5353 (README `REQ-RES-012`, `REQ-ADV-030`); in which no answer about
the printer is believed for longer than 120 seconds (`REQ-RES-013`); and whose
probe keeps only a reply from port 5353 that carries the identifier of its own
question (`REQ-SEC-018`). The section "Built with" named .NET SDK 10.0.403 and
the runtime Microsoft.NETCore.App 10.0.14, the latest .NET 10 release on both
of Microsoft's sources read that day, and the one 0.5.0 carries. On FIOS-STB-01
a copy downloaded with `curl.exe` passed both checks of
[`operating.md`](../operating.md), carried the runtime 10.0.14 and the stamp of
the tagged commit, was the same as the zip file by file, and was installed by
the "From a release" update steps with the signature check, in place of the
unsigned build of `0b51fcc`, which had run there since 2026-10-28. The
household had no printer for 12 seconds. The first line the new service logged
was its version, an iPhone read the printer's supply levels through it, and
the release's own probe, run on FIOS-STB-01, kept the printer's replies and
ignored nothing the second time it was run; the first time the printer did not
answer it. No page was printed through 0.6.0 in this walk. No code changed
after the tag.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name or the printer's certificate fingerprint.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
the commands run on FIOS-STB-01, from Git, or from GitHub's pages. Times in am
or pm are US Eastern, UTC-4; Edwin's are by his iPhone's clock, to the minute,
and that clock was not compared with FIOS-STB-01's. Everything here happened on
2026-10-30.

## What went into it

Sixteen commits came after the one `v0.5.0` names, up to and including the one
`v0.6.0` names. Four changed what a program does:

- `1b4358b`, of 2026-10-26: `REQ-RES-012`, a lookup takes only a multicast
  response from port 5353
  ([the finding](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md)).
- `de1d171`, of 2026-10-26: `REQ-RES-013`, no answer believed for longer than
  120 seconds
  ([the finding](2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md)).
- `0b51fcc`, of 2026-10-27: `REQ-ADV-030`, the conflict check takes only a
  multicast response from port 5353
  ([the finding](2026-10-27-the-conflict-check-takes-only-a-multicast-response-from-port-5353.md)).
- `ad99298`, of 2026-10-28: `REQ-SEC-018`, the probe keeps only a reply from
  port 5353 to its own question
  ([the finding](2026-10-28-the-probe-keeps-only-a-reply-from-port-5353-to-its-own-question.md)).

`49e8602` set the version. The other eleven changed documents: `e831fbb`,
`805a6bd`, `dcababb`, `a6f35bb`, `6fe0f49`, `098adff`, `fec29f7`, which
changed the header comment of `release.yml` and no step, `ef23654`, `9f58175`,
`26e6e2c` and `37994b7`. Nothing under `src` changed after `0b51fcc`.

The three changes to the service had run on a network before the release, in
two builds from source made on the development machine with SDK 10.0.403, so
carrying runtime 10.0.14, and not signed, each installed in turn as the service
on FIOS-STB-01: a build of `de1d171` from 2026-10-26, for 30 hours 20 minutes
([its finding](2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md)),
and a build of `0b51fcc` from 2026-10-28 to this install
([its finding](2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md)).
The change to the probe had run twice from a clone on the development machine,
on 2026-10-28 and 2026-10-29, and not from a release folder or on FIOS-STB-01.

## The decisions

The log of the build of `0b51fcc`, read today before anything else (that
finding's status note of 2026-10-30), showed 8 losses since the read before,
each ended by an answer, and 19 connections, all completed. Claude asked
whether to release 0.6.0 now or let the build run longer, and said it saw
nothing in the read that argued for waiting. Edwin: "yes please lets release
0.6.0". After the checks on FIOS-STB-01, Claude asked whether to install it
now in place of the build of `0b51fcc`, recommending it. Edwin: "lets install
it now thank you".

## The commit before the tag

**`49e8602`, the version.** `<Version>` set to 0.6.0 in
`Directory.Build.props`; the 0.6.0 entry in `CHANGELOG.md` and a sentence at
its top; a row for 0.6.0 in the table of versions in `SECURITY.md`, with 0.6.0
in its first sentence and `v0.6.0` in its paragraph on tags, and a sentence in
the rows for 0.5.0 to 0.1.0 naming the four problems 0.6.0 corrects; a sentence
in README section 12 and in `docs/operating.md` saying 0.6.0 is the sixth
version meant as a release; in README, the four Known problems entries and the
notes on `REQ-ADV-030`, `REQ-RES-012` and `REQ-RES-013`, which said no release
carries the change, made to say that 0.6.0 is the first to carry it; and the
status note of 2026-10-30 on the finding of the build of `0b51fcc`. Each
sentence on 0.6.0 was worded to be true before and after publishing. No code
changed besides the version.

On the development machine, before the commit, Edwin measured: Git's view (six
files, 302 lines added, 32 removed), a build with 0 errors, 598 tests passing
(28, 27, 49, 62, 86, 212 and 134, none failed or skipped), and SpecCheck 112
requirements, 23 assemblies, 18 evidence rows, 605 test records, 96
requirements with a passing test, 95 covered by code and 17 by evidence, 109
binding and all covered, 0 binding gaps, no orphaned or malformed markers,
duplicate IDs or orphaned evidence, 0 marked not run, 0 stale, `PASS`, exit
code 0. Before he committed, Claude had placed the six files through the link
to the development machine, after checking that its copies were the committed
ones byte for byte, and read them back the same; and had worked out the tree
the commit would have, `15100db9418829a91671b1bd6c2132b37a24fb39`. After the
commit, Claude read the commit object through the link: its parent was
`37994b7`, its tree that one, and its message the one given, character for
character. Edwin pushed it. CI run #182 passed in 1 minute 17 seconds and
CodeQL run 24 in 2 minutes 10 seconds. A fresh clone after the push matched
Claude's copy in all 249 files.

The step of `release.yml` that checks a tag against the version and the
changelog was not run as written: running PowerShell in Claude's workspace was
refused by that workspace's own safety check on 2026-10-29, and was not tried
again. Claude did the same check in Python on the files instead: one
`<Version>`, 0.6.0, and one heading for 0.6.0 in `CHANGELOG.md`, with 149 lines
of notes. The run below did the real check.

Edwin created the annotated tag `v0.6.0` on the commit, named by its full hash,
with the message "SecretPrinter 0.6.0, the sixth version meant as a release.",
at 19:02:04Z, and pushed it. Read back from GitHub in Claude's workspace, the
tag names `49e8602a5b192ec856c0d6232c4519f0240c41e1`. He approved the signing
job when it waited.

## The run that published it

Release run #9, read from the run's page in the desktop app's browser pane:

| | |
| --- | --- |
| Result | Success, 8 min 20 s, the wait for approval included: build 1 min 55 s, sign 2 min 9 s, publish 14 s |
| Approval | by `edwest19`, environment `release`, comment "reviewed" |
| Kept | `build-record` 34.4 KB, `signed-release` 35.7 MB, `unsigned-folder` 35.6 MB |
| Annotations | one notice, "Built with": ".NET SDK 10.0.403. The folder carries Microsoft.NETCore.App 10.0.14." |
| Signing job's summary | tag `v0.6.0`, the commit above, 216 files in the folder, 12 signed in this run under `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, the zip's SHA-256 as below, specification check `pass` |
| Build job's summary | the coverage matrix, "95 of 112 requirements fully covered" |

The run page gives 19:02:10Z as the time the push triggered it. Read three
times in the next few minutes, it said `Queued`; read again a minute or so
later, the build job had passed and the signing job was waiting. Claude told
Edwin in between that earlier runs had gone straight in; that was not checked,
and is not known.

The release page: 0.6.0, marked **Latest** and not a pre-release, on commit
`49e8602`, released by `github-actions` at 19:10:27Z, with the 0.6.0 entry of
`CHANGELOG.md` as its notes, followed by the section "Built with": ".NET SDK
10.0.403, as `dotnet --version` named it on the build machine." and "The .NET
runtime in the folder: Microsoft.NETCore.App 10.0.14, as
`SecretPrinter.Service.runtimeconfig.json` names it." Its two files, both
uploaded at 19:10:26Z:

| File | Size | SHA-256 as GitHub shows it |
| --- | --- | --- |
| `SecretPrinter-0.6.0-win-x64.zip` | 35.8 MB | `4dcf9a67a4868091fa1f04245aae9c95edd62cb084899597a95cde72a31d2fae` |
| `SecretPrinter-0.6.0-win-x64.zip.sha256` | 99 bytes | `a14143a6d10804b797fc6e70718e1d1ae7ea478bb988375d4f975d95eef69a19` |

A copy of both, downloaded in Claude's workspace with `curl`, gave the same two
digests. The zip held 216 files, 82,670,051 bytes unpacked, 202 of them `.exe`
or `.dll` files and 12 of those SecretPrinter's; its
`SecretPrinter.Service.runtimeconfig.json` named
`Microsoft.NETCore.App 10.0.14`; and `SecretPrinter.Service.dll` and
`SecretPrinter.Probe.dll` each carried the stamp
`0.6.0+49e8602a5b192ec856c0d6232c4519f0240c41e1`, read from their bytes.
Nothing in it was run there, and the signatures cannot be read there.

## The runtime it carries

Before the version was set, Claude read Microsoft's .NET release index on
GitHub (`release-notes/releases-index.json` in dotnet/core) and Microsoft's
download page for .NET 10, through a fetch tool. Both gave 10.0.14, of
2026-10-25, as the latest .NET 10 release, with SDK 10.0.403; the index marks
it a security release, and the page lists SDK 10.0.114 beside 10.0.403. The
entry says so and does not say what the build would carry, which the section
"Built with" says: 10.0.14. `0.5.0` carries the same runtime; `0.3.0` and
`0.4.0` carry 10.0.13, and `0.1.0` and `0.2.0` 10.0.12.

## The checks on FIOS-STB-01

All in Edwin's SSH session on FIOS-STB-01, which runs Windows 10, by one
command, read-only but for the two files it downloaded and the folder it made.
It would have stopped, downloading and unpacking nothing, had either file or
the folder already existed, and would have unpacked nothing had either download
failed. Claude did not run it first: it was checked by reading, and its
brackets and quotes were counted by a script.

**The download.** Both `curl.exe` commands ended with exit code 0. The zip was
37,590,553 bytes.

**Check 1, as printed.** `Get-FileHash` gave
`4DCF9A67A4868091FA1F04245AAE9C95EDD62CB084899597A95CDE72A31D2FAE`, and the
`.sha256` file held the same 64 characters followed by the file name. It is
also GitHub's digest above, and the one the signing job's summary gives.

**The unpacking.** Into a new folder, `sp-0.6.0`, in Edwin's user folder.

**Check 2, as printed,** on that folder: four lines, every one `Valid`:

| Files | Ours | Signer |
| --- | --- | --- |
| 185 | no | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | no | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | no | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 12 | yes | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |

No program file lacked a timestamp. The folder held 216 files and named its
runtime as `Microsoft.NETCore.App 10.0.14`, and the service's and the probe's
stamps both read `0.6.0+49e8602a5b192ec856c0d6232c4519f0240c41e1`, the commit
the tag names.

**The folder against the zip.** For each of the zip's 216 files, the SHA-256 of
the file inside the zip and of the same file in the folder: **all 216 were the
same, none was missing, and the folder held no other file.**

## The install

One command, run in the same session, which is elevated, did steps 2 to 5 of
"From a release" with step 4's signature check:

- It first checked that the session was elevated and that the new folder's
  stamp was the one above, and would have touched nothing had either failed.
  Both held. Unlike the command for 0.5.0, it did not first check that the
  printer answered on TCP port 631: that needs the printer's address, which
  Claude did not have. Had the printer not been answering, the service would
  have started in its waiting state.
- Step 2: printed the stamp installed, stopped the service, checked that it
  had stopped, removed the program folder, and confirmed it gone.
- Step 3: copied the new folder in: 216 files.
- Step 4: ran the signature check on the installed folder, which gave the same
  four lines, and read the stamp. It would have left the service stopped
  unless the stamp was the one above, every program file was `Valid`, and all
  twelve of SecretPrinter's were signed under the name above. All three held.
- Step 5: started the service and, 20 seconds later, printed its state,
  `Running`, and the log since the stop, without the indented listings of the
  advertisement, with IPv4 and link-local addresses, the printer's host name,
  UUIDs and the pin masked.

The service's registration, configuration, log and firewall rules were not
touched. **What it replaced was the build of `0b51fcc`,** stamped
`0.5.0+0b51fccbe685bafc780bb9f13fad14948f1412d0`, which was not signed and
carried .NET 10.0.14.

| Time | What the service logged |
| --- | --- |
| 19:29:40Z | The build of `0b51fcc`: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 19:29:48Z | **`SecretPrinter.Service version 0.6.0+49e8602a5b192ec856c0d6232c4519f0240c41e1`**, then `starting under the service control manager`, its configuration, and the resolver's socket on the Wi-Fi |
| 19:29:49Z | The responder's sockets on the Ethernet; the printer's capabilities and connection answered, each `TTL 120s)` with nothing capped; probing for the advertised names |
| 19:29:50Z | `Probe clear: no other device answered for the advertised names.` |
| 19:29:52Z | `Announced. Answering queries.`, then accepting print jobs on the client interface's IPv4 address and on its link-local address |

**The household had no printer for 12 seconds,** from 19:29:40Z to 19:29:52Z.
No warning or error was logged.

## What the build said when it stopped

Its stop summary at 19:29:40Z counts from its only start, at 00:58:49Z on
2026-10-28, 2 days 18 hours 30 minutes 51 seconds before:

```
Served 247 quer(ies) of 22463 seen; 22125 were for other services and were ignored; 90 carried every answer already and were not answered (RFC 6762 s7.1); 1 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); 0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); 193 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); 182 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).
By transport: IPv4 answered 197 of 12099 seen; IPv6 answered 50 of 10364 seen.
```

247, 22,125, 90, 1 and 0 add up to 22,463, and the two transports to 247 and
22,463. Its log to 16:59:43Z today, read before the release, is in the status
notes on [its finding](2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md):
17 losses, each ended by an answer to one of its own questions, and 86
connections, of which 83 completed and 3 failed on 2026-10-28. Its last 2
hours 30 minutes, from 16:59:44Z to the stop, were not read line by line.

## The iPhone

Edwin opened the printer's supply levels on the iPhone at 3:32 pm by its clock,
and sent two screenshots: "yes supplies worked". The first shows the printer
list with two rows, `SecretPrinter (ET-3760)`, with the line "SecretPrinter
proxy. Capabilities read from the printer at 2026-10-30 19:29:48Z", the second
in which the new service logged that it was asking the printer, and the
printer's own entry, `EPSON ET-3760 Series`. The second shows a page headed
"Printer Info", naming the printer `EPSON ET-3760 Series`, with supply levels.
Which row that page was opened from is not shown; on 2026-10-08 the
information button beside the proxy's row opened a page giving the same name,
with the printer's ink levels (`docs/operating.md`, "What you will see on an
iPhone"). The log below shows the supply levels were read through the service.

The log from 19:29:40Z, read at 19:35:32Z with one read-only command: 88
lines, to 19:33:05Z. 37 are not `Job:` lines, each logged once, all of them
the stop and start above; 35 are the indented listing lines the read leaves
out (`record`, `publish` and the like);
and 16 are the `Job:` lines of 4 connections, accepted at 19:32:13Z and
19:32:14Z, every one from the same IPv6 link-local address, located from the
resolver's cache, relayed over TLS 1.2 with the printer's certificate matching
the pinned fingerprint, to one printer endpoint, and completed between
19:32:45Z and 19:33:05Z, after 31.02 to 51.72 seconds. They carried 657, 2,628,
5,387 and 12,223 bytes to the printer and 498, 1,992, 5,632 and 14,108 back. No
connection failed or was refused, no loss was logged, and no warning or error.

## The release's probe on FIOS-STB-01

The probe of the release, `SecretPrinter.Probe.exe` in `sp-0.6.0`, was run
twice in Edwin's SSH session, on FIOS-STB-01's printer-side Wi-Fi, with the
service running, by a command that masked addresses, the printer's host name,
UUIDs, hardware addresses and the value of the TXT key `note`. It is the first
time the probe that `REQ-SEC-018` changed ran from a release folder, and the
first time on FIOS-STB-01.

- **The first run got nothing:** "No replies were received." in the 5 seconds
  of its first round. The printer was on offer the whole time; the service
  logged no loss. Why the printer did not answer that question is not shown.
- **The second run got both replies and kept both:** round 1, 1,412 bytes from
  the printer with 5 answers and 11 additionals, naming 4 instances; round 2,
  1,344 bytes with 7 answers and 4 additionals. **No line said a datagram was
  ignored.** The report listed 5 service types, `_ipp._tcp`, `_ipps._tcp`,
  `_pdl-datastream._tcp`, `_printer._tcp` and `_universal._sub._ipp._tcp`, with
  the instances `EPSON ET-3760 Series._ipp._tcp.local` and
  `EPSON ET-3760 Series._ipps._tcp.local` among them; "Summary: 2 repl(ies), 16
  record(s), 5 service type(s) seen."; exit code 0. As in the usual run of
  2026-10-29, the `_ipps._tcp` TXT record was not received; the `_ipp._tcp` one
  had its 21 entries.

Between the two runs Edwin ran a status check of his own, which is not part of
SecretPrinter. It printed the service's last log lines and its own lines
without masking, so the paste held FIOS-STB-01's and the iPhone's link-local
addresses and machine addresses on both networks. They were in the
conversation only and are not in this repository. It counted, for the last 24
hours, 8 losses and 23 connections, all completed, as the two reads of today
do.

## What Claude predicted, and what it did not

Held: every number measured before the commit, Git's view of it, its tree, the
push, CI and CodeQL passing; the tag and its push; the run passing, with SDK
10.0.403 and runtime 10.0.14, and waiting for approval; the release marked
Latest and not a pre-release, with its zip and `.sha256` file; on FIOS-STB-01,
both exit codes 0, a zip of 37,590,553 bytes, check 1 the same digest, check 2
four lines, all `Valid`, 185, 2, 3 and 12, none without a timestamp, 216 files,
runtime 10.0.14, both stamps, and the folder the same as the zip in all 216
files; the install's stamp before, 216 files copied, the same four lines, the
stamp after and `Running`; the log from the stop of `0b51fcc` to
`Accepting print jobs`, the version line first and nothing capped; the printer
off offer for 5 to 15 seconds, at 12; for the supply check, 2 to 10
connections, at 4, all from one link-local address, from the cache, with TLS
1.2 and the pin matched, to one endpoint, none failed, none of a million bytes,
and no loss; for the probe's second run, both instances, nothing ignored, exit
code 0, and the `note` masking working. Said in advance to be unknown: the
order of check 2's lines, the commit's hash, and whether the printer would
answer the probe.

Not held:

- **The probe's first run.** Claude predicted the printer's replies would be
  kept; it got none. Claude had said the printer might be quiet, and the
  second run got them.
- **A run "Queued" for minutes,** and earlier runs that "went straight in".
  Claude said both to Edwin while the run page showed `Queued`; the second was
  not checked. Neither was a prediction about the result, which held.

## Sentences the release made exact

Sentences written in `49e8602` to be true before and after publishing, which
said 0.6.0 is the sixth version meant as a release, or the first meant as one
to carry `REQ-RES-012`, `REQ-RES-013`, `REQ-ADV-030` or `REQ-SEC-018`, are made
exact by the commit that adds this finding, each with a note giving what it
said, as for each release before: in `CHANGELOG.md`, `README.md` (section 12,
the notes on `REQ-ADV-030`, `REQ-RES-012` and `REQ-RES-013`, and the four
Known problems entries), `SECURITY.md` and `docs/operating.md`.

## What this does not show

- **A page printed through 0.6.0.** The iPhone read supply levels through it;
  no page was printed in this walk. The same service code printed a page
  through the build of `0b51fcc` on 2026-10-27, at the first attempt.
- **0.6.0 over time.** One start and one supply check, within minutes of the
  install.
- **A response or reply that one of the three checks ignores, on a network.**
  None was seen to arrive; the service does not log one it ignores, and the
  probe logged none.
- **A Windows shutdown or restart with 0.6.0 running,** or a start of 0.6.0 at
  boot.
- **0.6.0 on Windows 11, or on a machine with no .NET.** FIOS-STB-01 has .NET
  SDKs installed; the release carries its own runtime.
- **A download with a browser.** Both checks were run on a copy `curl.exe`
  fetched.
- **The last 2 hours 30 minutes of the build of `0b51fcc`,** line by line.

## How it was read

Commands given to Edwin, each read-only except the download and the install,
run by him in an SSH session on FIOS-STB-01 or in PowerShell on the development
machine, and pasted or attached. None was run through PowerShell's parser
first; each was checked by reading. Files were placed on the development
machine, and the commit read back, through the link of the Claude desktop app.
GitHub's run and release pages were read through the browser pane of the
Claude desktop app on the development machine; nothing was clicked or changed
there. Microsoft's release index was read from GitHub in Claude's workspace, and
its download page through a fetch tool. The commands masked link-local and
other IPv6 addresses, IPv4 addresses, the printer's host name, UUIDs and the
pin; the status check of Edwin's own, above, did not. The arithmetic was done
in Claude's workspace.
