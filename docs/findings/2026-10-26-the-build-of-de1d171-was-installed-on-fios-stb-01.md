# The build of de1d171 was installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-26, from a run made that afternoon. Reviewed by a human before
merge.*

**Status: run once on a network, on 2026-10-26. A build of `de1d171`, which
carries the two changes of that day, `REQ-RES-012` (a lookup takes only a
multicast response from port 5353) and `REQ-RES-013` (no answer believed for
more than 120 seconds), was made on the development machine with .NET SDK
10.0.403, so that it carries runtime 10.0.14 like release 0.5.0. It is not
signed. It was copied to FIOS-STB-01, checked there, and installed in place of
release 0.5.0 at 18:38:39Z. Its two startup lookups were answered in the same
second, each with a lifetime of 120 seconds and nothing capped, and the
printer was offered again 6 seconds after the old service stopped. An iPhone's
first attempt to print said it could not contact the printer, while the
service relayed every connection to the printer and logged no failure; the
second attempt printed a page. No release carries these changes.**

*(Status note, 2026-10-27, by Claude, Claude Opus 5.5: this build's first 26
hours on FIOS-STB-01 were read there on 2026-10-27, read-only, from the
service's log, from the build's first line at 18:38:40Z on 2026-10-26 to the
reading at 20:52:29Z on 2026-10-27: 26 hours 13 minutes 49 seconds. Nothing
was run, restarted or changed for it. Windows last started at 20:55:31Z on
2026-10-14, the service was running, and the log holds one start in the
period, this build's, stamped `0.5.0+de1d17138166363d4b291cffa3c323486f798d01`.
No line in the period says `capped from`. It accepted 27 connections, the
eleven below among them, and refused none; each was located from the
resolver's cache, relayed over TLS 1.2 with the printer's certificate matching
the pinned fingerprint, and completed. None failed. Every client address was
the same IPv6 link-local address. Only connection 9 below sent a million bytes
or more; which of the others carried pages is not in the log. **The service
lost the printer 13 times and found it again each time without a restart,
each time by one of its own questions being answered; it heard the printer
without being asked in none of them.** This build takes an answer only by
multicast from port 5353 (`REQ-RES-012`), so the printer went on answering
that way. Eight of the losses ended with `it answered` 0 to 3 seconds after a
question was due by the schedule under "The arithmetic" in the
[finding of 2026-10-22](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md),
counted from the loss. The other five ended after a `The printer-side
interface is usable again` line, which starts the questions over on the same
schedule, and each ended 0 or 1 second after a question due counted from that
line: three at the first question, two at the one due 35 seconds after it. In
one of those five, at 12:10:51Z on 2026-10-27, in the second the printer was
lost, the service logged `Could not reopen the printer-side socket: Interface
'Wi-Fi' holds … but is not up`, and at 12:10:57Z that the interface was usable
again and the socket reopened. From the code, the service reopens that socket
before each question it puts to a printer it holds unreachable. Claude had
predicted no such line. The printer was not offered for 3 hours 13 minutes 42
seconds in all, counted from each `Printer unreachable` line to its
`Advertisement restored` line, 12.3% of the period. The two longest losses,
from 05:34:25Z and from 18:49:16Z on 2026-10-27, kept it off offer for 1 hour
9 minutes 21 seconds each, each ending at the question due 4,155 seconds after
the loss.
Why the printer went unanswered in any loss is not shown. The build of
`5a291c4`, read the same way over 71 hours from 2026-10-22, did not offer the
printer for 9.1% of its period; those were other days, and nothing here
attributes the difference to either build. No warning or error was logged but
those of the losses and the one line above: no `Printer lookup failed` line.
The read was two commands, run by Edwin in his SSH session. The first grouped
every line that is not a `Job:` line, with addresses, the printer's host name,
UUIDs, the pin and numbers masked, counted the `Job:` lines, and listed each
loss with how it ended; it printed only the first and last time of each group,
so the second printed the time of each interface line. The arithmetic was done
in Claude's workspace.)*

*(Status note, later on 2026-10-27, by Claude, Claude Opus 5.5: a build of
`0b51fcc`, which also carries `REQ-ADV-030`, replaced this build on FIOS-STB-01
at 00:58:46Z on 2026-10-28, a little over 30 hours after it started; an iPhone
printed through it. This build's stop summary counted 71 queries served of
10,109 seen. The 4 hours 6 minutes from the read above to the stop were not
read. See [the finding of that run](2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md).)*

*(Status note, 2026-10-28, by Claude, Claude Opus 5.5: the 4 hours 6 minutes 17
seconds from the read at 20:52:29Z on 2026-10-27 to this build's stop at
00:58:46Z on 2026-10-28 were read on FIOS-STB-01 on 2026-10-28, read-only, in
the same read as the first 16 hours of the build of `0b51fcc`. In them the
service lost the printer once, at 21:02:35Z on 2026-10-27, and found it again
without a restart when the question due 162 seconds after the loss was
answered, 2 seconds after it was due; the printer was off offer for 2 minutes
47 seconds. Its probe before offering the printer again came back clear. The
build accepted no connection in that time. Over its whole run on FIOS-STB-01,
30 hours 20 minutes 6 seconds, it lost the printer 14 times, each ended by one
of its own questions being answered and none by the printer being heard without
being asked; it did not offer the printer for 3 hours 16 minutes 29 seconds,
10.8% of the run; and it accepted 27 connections, none of which failed or was
refused. See the status note of 2026-10-28 on
[the finding of the build of `0b51fcc`](2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md).)*

*(Status note, 2026-10-30, by Claude, Claude Opus 5.5: release 0.6.0, published
that day, carries the two changes this build ran, `REQ-RES-012` and
`REQ-RES-013`, and `REQ-ADV-030` and `REQ-SEC-018` besides. See
[the finding of the sixth release](2026-10-30-the-sixth-release-was-published-and-installed-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name, the certificate pin or a user name. The log lines quoted
were printed on FIOS-STB-01 with link-local addresses, the printer's host name,
UUIDs and the pin replaced before they were pasted.

**About the times.** Times ending in `Z` are UTC, from FIOS-STB-01's log. Times
Edwin gave are Eastern Daylight Time, four hours behind UTC, from the iPhone;
the two clocks were not compared.

## Why this was run

The two changes of 2026-10-26 were measured against the code by tests and had
not run on a network
([REQ-RES-012](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md),
[REQ-RES-013](2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md)).
The risk the first carries is a lookup that hears nothing: if the printer ever
answered the service's questions by unicast, the build would never find it.

## Decided

Edwin West chose, each from options Claude put to him that day:

- After `1b4358b` was pushed: "Wait for the next change", over building it on
  FIOS-STB-01 at once or a brief run on the development machine.
- After `de1d171` was pushed: "Build on FIOS-STB-01 (Recommended)", over a
  brief run on the development machine or not yet.
- After FIOS-STB-01 was found to have SDKs 10.0.301 and 10.0.401, the newer of
  which builds with runtime 10.0.12, two security updates behind the 10.0.14
  that release 0.5.0 carries there: "Update dev box SDK (Recommended)", over
  installing a newer SDK on FIOS-STB-01, where he had decided on 2026-10-09 to
  leave the SDK as it is, or building with 10.0.401 anyway.

## What ran

- **The SDK.** Microsoft's .NET 10.0 download page and its release metadata,
  both read through a fetch tool on 2026-10-26, gave 10.0.14 of 2026-10-25, a
  security release, as the latest .NET 10, and 10.0.403 as the latest SDK. The
  development machine's last restore had used SDK 10.0.303, read from the
  service project's restore file through the link to that machine. Edwin
  installed SDK 10.0.403 there from Microsoft's page; `dotnet --list-sdks` then
  listed 8.0.427, 9.0.320, 10.0.303 and 10.0.403. The new SDK's first-run
  message said it had "Installed an ASP.NET Core HTTPS development
  certificate", which is not trusted unless asked to be; nothing in this
  project uses it, and whether to remove it is Edwin's choice.
- **The build.** On the development machine, at `de1d171` with no local
  changes, `publish-release.ps1` made a folder in Edwin's profile: 216 files,
  82,480,839 bytes, `SecretPrinter.Service.exe` and `SecretPrinter.Probe.exe`
  each "carrying Microsoft.NETCore.App 10.0.14", and the stamp
  `0.5.0+de1d17138166363d4b291cffa3c323486f798d01`: the version number has
  not changed since 0.5.0, and the commit follows the `+`.
- **The copy.** The folder was zipped (36,261,899 bytes) and its SHA-256 noted,
  then copied with `scp`. The first `scp` was typed without the colon after the
  host name, so it copied the zip to a file in the development machine's
  working folder, named after the host; Claude read that file through the
  link, found its SHA-256 the zip's, and Edwin removed it. The second `scp`
  sent it to FIOS-STB-01. There, one command printed the zip's SHA-256, the
  same as on the development machine, unpacked it into a new folder in Edwin's
  profile, and counted 216 files and 82,480,839 bytes with the same stamp.
- **The install.** One command, run by Edwin in an SSH session on FIOS-STB-01,
  checked the new folder's stamp before stopping anything; stopped the
  service; removed `C:\Program Files\SecretPrinter` and checked it was gone;
  copied the new folder in; checked the installed stamp before starting the
  service; waited 15 seconds; and printed the service's lines since the stop.
  Unlike the install of 2026-10-22, it did not test the printer's port first:
  Claude did not have the printer's address and did not guess it.

## The swap

| Time | Lines, shortened |
| --- | --- |
| 18:38:39Z | Release 0.5.0: `Service 'SecretPrinter' stopping.`, `No longer accepting print jobs on Ethernet.`, `Advertisement retracted (goodbye records sent).`, its summary and `SecretPrinter stopped.` |
| 18:38:40Z | The build's first line: `SecretPrinter.Service version 0.5.0+de1d17138166363d4b291cffa3c323486f798d01` |
| 18:38:41Z | Its configuration; the resolver socket bound to port 5353 and joined to `224.0.0.251` on the printer-side Wi-Fi only; `Capabilities from EPSON ET-3760 Series._ipp._tcp.local at …:631 (host <printer-host>.local, TTL 120s).` and `Connections to EPSON ET-3760 Series._ipps._tcp.local at …:631 (host <printer-host>.local, TTL 120s).` |
| 18:38:42Z | `Probe clear: no other device answered for the advertised names.` |
| 18:38:44Z | `Announced. Answering queries.` |
| 18:38:45Z | `Accepting print jobs on` its IPv4 address and its link-local address |

The release's summary counted 74 queries served of 6,206 seen; since when it
had counted was not read.

## The page

Edwin, after it: "failed the first time at 2:41 "could not contact printer"
succeeded second time 2:42 and page printed".

The service's lines from 18:40:00Z, read twice, the second time at about
18:46Z: eleven connections, every one from an IPv6 link-local address, every
one located at once from the cached answer, relayed to the printer over TLS
1.2 with the certificate matching the pinned fingerprint, and completed. None
failed and none was refused.

| Connection | Accepted | Completed | Sent | Received |
| --- | --- | --- | --- | --- |
| 1 | 18:40:53Z | 18:41:45Z (51.59 s) | 1,694 | 2,112 |
| 2 | 18:40:54Z | 18:41:25Z (30.96 s) | 1,445 | 2,644 |
| 3 | 18:41:15Z | 18:41:46Z (30.97 s) | 847 | 1,056 |
| 4 | 18:41:15Z | 18:41:46Z (30.99 s) | 847 | 1,056 |
| 5 | 18:41:25Z | 18:44:57Z (211.22 s) | 75,160 | 60,068 |
| 6 | 18:41:25Z | 18:42:26Z (60.14 s) | 7,620 | 10,422 |
| 7 | 18:41:25Z | 18:42:21Z (55.70 s) | 4,730 | 5,134 |
| 8 | 18:41:25Z | 18:42:21Z (55.71 s) | 3,285 | 2,490 |
| 9 | 18:41:50Z | 18:43:09Z (78.98 s) | 1,072,628 | 9,684 |
| 10 | 18:41:55Z | 18:42:29Z (33.85 s) | 3,199 | 1,619 |
| 11 | 18:43:44Z | 18:44:14Z (30.07 s) | 657 | 498 |

"Sent" is what the iPhone sent through the service, in bytes. Connection 9
carried the page. No other connection carried more than 75,160 bytes.

The second read's paste had pieces of the command's own text joined to two
lines; the service cannot write that text, so it came from the copy, as on
2026-10-22.

## What this shows

1. **The printer answered the build's lookups by multicast from port 5353.**
   The build takes nothing else (`REQ-RES-012`), and both startup lookups were
   answered in the same second. The first time that has been shown on the
   network.
2. **The printer's answers have a lifetime of 120 seconds, and the cap left
   them alone** (`REQ-RES-013`): both startup lines end `TTL 120s).`, with no
   `capped from`.
3. **A later lookup was answered too.** The first connection, at 18:40:53Z,
   was located from the cache, 132 seconds after the startup answers; the
   build holds an answer for at most 120 seconds, so an answer obtained after
   18:38:53Z was in the cache. Worked out from the code; the log has no line
   for a routine question, and which question was answered is not shown. By
   the schedule of `REQ-RES-008`, the watch's first reconfirmation fell at 80
   to 82% of 120 seconds after 18:38:41Z.
4. **A page printed through it.**
5. **The first attempt fits a known problem.** README Known problems: "A print
   attempt can reach the printer and send no job." On 2026-10-01 and
   2026-10-02, in three attempts, an iPhone connected through the proxy, had
   every request answered, and sent no job; in two of them it showed a message
   that it could not reach the printer; a later attempt printed on both days.
   Here, every connection but the page's was relayed and completed, none
   carried more than 75,160 bytes, and the iPhone said it could not contact
   the printer. Which connections belong to the first attempt, and
   which to the print sheet opening, is not shown: the clocks were not
   compared. Why it happens is not established.

## What this does not show

- **Anything over hours or days.** The build is left running; its log will
  show how it fares. *(Status note, 2026-10-27, by Claude, Claude Opus 5.5:
  its first 26 hours were read; see the status note at the top.)*
- **That the printer's records heard between lookups are taken by this
  build.** The printer was not restarted.
- **That the first attempt's failure has nothing to do with these changes.**
  It fits a problem seen on builds without them; that is all.

## What Claude predicted, and what it did not

- **Did not hold:** that `Accepting print jobs on …` would come before
  `Announced. Answering queries.`; it came a second after.
- **Did not hold:** in the first reading of the page's lines, Claude counted
  eleven connections, ten completed. There were ten, nine completed.
- **Did not hold, earlier that day:** that `git add` would print nothing. It
  printed a warning for each file, that LF will be replaced by CRLF in the
  working copy; the committed files were then compared with Claude's, byte for
  byte, in a clone from GitHub.
- **Held:** the SDKs of FIOS-STB-01 included 10.0.401; the folder carried
  10.0.14 in 216 files; the zip's hash matched after the copy; the stamp; the
  0.5.0 stop lines; `TTL 120s).` with no `capped from`; the printer off offer
  for 5 to 10 seconds (6).

## Left as they are

On FIOS-STB-01, in Edwin's profile: the zip and the unpacked folder of
`de1d171`; the folders of `5a291c4` and of 0.5.0, and the 0.5.0 zip and its
`.sha256`, from before. On the development machine: the folder of `de1d171`
and its zip, and SDK 10.0.403. Going back to 0.5.0 would be the same swap with
the 0.5.0 folder; it has not been needed or tried.

## How it was measured

- Commands given by Claude one at a time and run by Edwin, on the development
  machine and in an SSH session on FIOS-STB-01; their output pasted.
- Files on the development machine read by Claude through the link to it:
  the restore file, and the file the first `scp` made.
- The SDK versions through the fetch tool, from two of Microsoft's sources.

## What changed

- This finding.
- `README.md`: the notes on `REQ-RES-012` and `REQ-RES-013`, the Known
  problems entries on them and on a print attempt that sends no job, and the
  section's note.
- Status notes on the findings of
  [REQ-RES-012](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md)
  and [REQ-RES-013](2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md).
