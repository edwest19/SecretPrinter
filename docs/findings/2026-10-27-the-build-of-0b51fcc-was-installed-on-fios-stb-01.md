# The build of 0b51fcc was installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-27, from a run made that evening. Reviewed by a human before
merge.*

**Status: run once on a network, on the evening of 2026-10-27 (Eastern). A
build of `0b51fcc`, which carries `REQ-ADV-030` (the conflict check takes only
a multicast response from port 5353) as well as `REQ-RES-012` and
`REQ-RES-013`, was made on the development machine with .NET SDK 10.0.403, so
that it carries runtime 10.0.14. It is not signed. It was copied to
FIOS-STB-01, checked there, and installed in place of the build of `de1d171`
at 00:58:46Z on 2026-10-28. Its probe at startup came back clear, its two
startup lookups were answered within about a second, each with a lifetime of
120 seconds and nothing capped, and the printer was off offer for 7 seconds. An
iPhone printed a page at the first attempt. No release carries these changes.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network, the
printer's host name, the certificate pin or a user name. The log lines quoted
were printed on FIOS-STB-01 with link-local addresses, the printer's host name,
UUIDs and the pin replaced before they were pasted.

**About the times.** Times ending in `Z` are UTC, from FIOS-STB-01's log; the
run fell on 2026-10-28 in UTC and on the evening of 2026-10-27 in Eastern
Daylight Time, four hours behind. Times Edwin gave are Eastern, from the
iPhone; the two clocks were not compared.

## Why this was run

`REQ-ADV-030` was measured against the code by tests and had not run on a
network
([finding](2026-10-27-the-conflict-check-takes-only-a-multicast-response-from-port-5353.md)).
After it was pushed, with CI run 172 passing, Edwin West chose "Run 0b51fcc on
FIOS-STB-01 (Recommended)", over keeping the build of `de1d171` and running
`0b51fcc` with a later change, or stopping for the day. Claude recommended it
so that the hours on FIOS-STB-01 before a next release cover all three changes
such a release would carry. The first 26 hours of the build of `de1d171` had
been read that afternoon
([finding](2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md),
status note of 2026-10-27).

## What ran

- **The build.** On the development machine, at `0b51fcc` with no local
  changes, `publish-release.ps1` made a folder in Edwin's profile: 216 files,
  82,481,347 bytes, 508 more than the build of `de1d171`;
  `SecretPrinter.Service.exe` and `SecretPrinter.Probe.exe` each "carrying
  Microsoft.NETCore.App 10.0.14"; and the stamp
  `0.5.0+0b51fccbe685bafc780bb9f13fad14948f1412d0`. The version number has not
  changed since 0.5.0, and the commit follows the `+`.
- **The copy.** The folder was zipped with `Compress-Archive` (36,262,049
  bytes) and its SHA-256 noted, then copied with `scp`. On FIOS-STB-01, one
  command printed the zip's SHA-256, the same as on the development machine,
  unpacked it into a new folder in Edwin's profile, and counted 216 files and
  82,481,347 bytes with the same stamp.
- **The install.** One command, run by Edwin in an SSH session on FIOS-STB-01,
  checked the new folder's stamp before doing anything; printed the stamp
  installed, `0.5.0+de1d17138166363d4b291cffa3c323486f798d01`; stopped the
  service and checked it had stopped; removed `C:\Program Files\SecretPrinter`
  and checked it was gone; copied the new folder in; checked the installed
  stamp before starting the service; waited 15 seconds; printed the service's
  state, `Running`; and printed the service's lines since the stop. Each check
  that failed would have stopped it there and said why; none did.

## The swap

| Time | Lines, shortened |
| --- | --- |
| 00:58:46Z | The build of `de1d171`: `Service 'SecretPrinter' stopping.`, `No longer accepting print jobs on Ethernet.`, `Advertisement retracted (goodbye records sent).`, its summary, and `SecretPrinter stopped.` |
| 00:58:49Z | The new build's first line: `SecretPrinter.Service version 0.5.0+0b51fccbe685bafc780bb9f13fad14948f1412d0`; its configuration; the resolver socket bound to port 5353 and joined to `224.0.0.251` on the printer-side Wi-Fi only; `Asking … what it can do, and … where to connect` |
| 00:58:50Z | The responder's two sockets; `Capabilities from EPSON ET-3760 Series._ipp._tcp.local at …:631 (host <printer-host>.local, TTL 120s).` and `Connections to EPSON ET-3760 Series._ipps._tcp.local at …:631 (host <printer-host>.local, TTL 120s).`; `Probing for the advertised names before offering them (RFC 6762 s8.1).` |
| 00:58:51Z | `Probe clear: no other device answered for the advertised names.` |
| 00:58:53Z | `Announced. Answering queries.`, then `Accepting print jobs on` its IPv4 address and its link-local address |

The summary of the build of `de1d171` at its stop: "Served 71 quer(ies) of
10109 seen; 10019 were for other services and were ignored; 19 carried every
answer already and were not answered (RFC 6762 s7.1)"; none was held back by
the one-second rule or refused for an address off the network; "59 of those
served were answered by unicast because they asked for it", and "67 of those
served were answered after a random wait". By transport: "IPv4 answered 62 of
5230 seen; IPv6 answered 9 of 4879 seen". Those counters are kept by the one
responder each start of the service makes (`ServiceHost.cs`, read on
2026-10-27), so they cover that build's whole run, from 18:38:40Z on
2026-10-26, a little over 30 hours.

## The page

Edwin, after it: "printed a page and it came out right away 9:02pm edt no
contact printer issue this time".

The service's lines from 00:58:54Z, read once, a few minutes after: five
connections, every one from an IPv6 link-local address, every one located at
once from the cached answer, and relayed to the printer over TLS 1.2 with the
certificate matching the pinned fingerprint. None failed, none was refused, and
no line but `Job:` lines was logged.

| Connection | Accepted | Completed | Sent | Received |
| --- | --- | --- | --- | --- |
| 1 | 01:01:51Z | still open when read | | |
| 2 | 01:02:04Z | 01:02:38Z (34.31 s) | 2,628 | 1,992 |
| 3 | 01:02:04Z | 01:02:38Z (34.31 s) | 3,416 | 4,138 |
| 4 | 01:02:07Z | 01:03:06Z (58.33 s) | 851,042 | 6,137 |
| 5 | 01:02:12Z | 01:02:44Z (32.51 s) | 2,512 | 1,266 |

"Sent" is what the iPhone sent through the service, in bytes. Connection 4
carried the page; no other carried more than 3,416 bytes. The page of
2026-10-26 was 1,072,628 bytes; the two pages were not the same and were not
compared.

The paste had pieces of the install command's own text joined to four lines;
the service cannot write that text, so it came from the copy, as on 2026-10-22
and 2026-10-26.

## What this shows

1. **The conflict check, now held to RFC 6762 §6, found nothing on this
   network.** The probe at startup came back clear, as every probe did in the
   reads of FIOS-STB-01's log recorded on 2026-10-25 and 2026-10-27, where no
   conflict was logged. That is what was expected. The change makes the check
   read less, and nothing here shows that a response it now ignores ever
   arrived; an ignored response is not logged.
2. **The printer answered the build's lookups by multicast from port 5353,
   with a lifetime of 120 seconds,** as on 2026-10-26 (`REQ-RES-012`,
   `REQ-RES-013`).
3. **A page printed through it at the first attempt.**

## What this does not show

- **Anything over hours or days.** The build is left running; its log will
  show how it fares.
- **The last hours of the build of `de1d171`,** from the read at 20:52:29Z on
  2026-10-27 to its stop at 00:58:46Z on 2026-10-28, 4 hours 6 minutes, were
  not read.

## What Claude predicted, and what it did not

- **Held:** the build's 216 files, 10.0.14 in both programs, and its stamp;
  bytes within a few kilobytes of the build of `de1d171`; a zip of about
  36.26 MB; the same hash, count, bytes and stamp on FIOS-STB-01; the two
  stamps the install printed and `Running`; the stop lines; `TTL 120s).` with no
  `capped from`; `Probe clear`; `Announced. Answering queries.` before the
  `Accepting print jobs` lines; the printer off offer for 5 to 10 seconds (7);
  the page printing; every connection from a link-local address, located from
  the cache, relayed over TLS 1.2 with the pin matching, and none failing.
- **Did not hold:** that the page's connection would carry about a million
  bytes, as on 2026-10-26; it carried 851,042. And that every connection would
  have completed when read; the first was still open.

## Left as they are

On FIOS-STB-01, in Edwin's profile: the zip and the unpacked folder of
`0b51fcc`; those of `de1d171`; and from before, the folders of `5a291c4` and of
0.5.0, and the 0.5.0 zip and its `.sha256`. On the development machine: the
folder of `0b51fcc` and its zip, beside those of `de1d171`. Going back to the
build of `de1d171`, or to 0.5.0, would be the same install command with that
folder and its stamp; it has not been needed or tried.

## How it was measured

- Commands given by Claude one at a time and run by Edwin, on the development
  machine and in an SSH session on FIOS-STB-01; their output pasted.
- The install command was checked by Claude, before it was given, to parse as
  PowerShell, with the parser of PowerShell 7.4.6 in Claude's workspace; it was
  not run there.

## What changed

- This finding.
- `README.md`: the note on `REQ-ADV-030` and its Known problems entry now say
  it was run once on a network, each with what it said before; the paragraph
  on FIOS-STB-01 gains this build; and the note of the Known problems section.
- Status notes on the findings of
  [REQ-ADV-030](2026-10-27-the-conflict-check-takes-only-a-multicast-response-from-port-5353.md)
  and of [the build of `de1d171`](2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md).
