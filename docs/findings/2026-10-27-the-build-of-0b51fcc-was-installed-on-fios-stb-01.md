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

*(Status note, 2026-10-28, by Claude, Claude Opus 5.5: this build's first 16
hours on FIOS-STB-01 were read there on 2026-10-28, read-only, from the
service's log, from the build's first line at 00:58:49Z to the reading at
16:59:03Z: 16 hours 0 minutes 14 seconds. A second read, at 17:08:48Z, printed
every line from 16:21:12Z with its time. Nothing was run, restarted or changed
for them. Windows last started at 20:55:31Z on 2026-10-14, the service was
running, and the log holds one start in the period, this build's. No line in
the period says `capped from` or `Printer lookup failed`, and none reports a
conflict. **The service lost the printer 6 times and found it again each time
without a restart, each time by one of its own questions being answered; it
heard the printer without being asked in none of them.** Each loss ended with
`it answered` 2 or 3 seconds after a question due by the schedule under "The
arithmetic" in the
[finding of 2026-10-22](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md),
counted from the loss: the questions due 162, 556, 1,073, 295, 2,102 and 295
seconds after the losses at 08:24:47Z, 10:03:41Z, 10:23:08Z, 14:15:58Z,
15:27:44Z and 16:16:12Z. No `The printer-side interface is usable again` line
and no `Could not reopen` line was logged. The printer was not offered for 1
hour 15 minutes 15 seconds in all, counted from each `Printer unreachable` line
to its `Advertisement restored` line, 7.8% of the period; the longest loss,
from 15:27:44Z, kept it off offer for 35 minutes 8 seconds. Before offering the
printer again after each loss, the service probed for its names, as it did at
startup: seven probes, each logged `Probe clear`, and no conflict was logged at
any other time, so the check that `REQ-ADV-030` changed found none. To the
second read the build accepted 31 connections and refused none. Every one came
from an IPv6 link-local address, the 22 to the first read all from the same one
(the second read hid the address), and every one was located from the
resolver's cache: the five of the page below, which all completed; 17 between
16:51:37Z and 16:57:21Z; and 9 between 17:03:18Z and 17:05:14Z. 28 were relayed
over TLS 1.2 with the printer's certificate matching the pinned fingerprint,
and completed; none carried a million bytes or more. **Three failed.** They
were accepted together at 16:57:21Z and each failed at 16:57:31Z, after 10.02
or 10.03 seconds, with no bytes relayed either way: `Could not connect to the
printer at …:631: The TLS handshake with the printer at …:631 did not complete
within 10s.` From the code (`TlsConnectionFactory.ConnectAsync`), that message
is written only after the TCP connection to the printer's address has been
made, when the 10 seconds allowed for the connection and its handshake together
run out during the handshake. So each time a TCP connection was made and the
handshake was not finished in what was left of the 10 seconds. The printer had
been on offer since 16:21:12Z, and its answer was in the resolver's cache,
which holds one for at most 120 seconds (`REQ-RES-013`). One connection,
accepted at 16:52:10Z, was open the whole time; it completed at 16:57:31Z,
after 320.98 seconds, with 109,985 bytes sent and 86,662 received, logged just
after the three failures in the same second. At 17:05:14Z three connections
were opened while another long one was open, and all three completed. Why those
three handshakes were not finished is not shown. One TLS handshake with the
printer had timed out before, on 2026-10-02: item 9 under "Also seen" in
[that day's finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md).
Edwin said that in this time he had been opening the printer's supply levels on
the iPhone, through SecretPrinter, to see that it was working, and had printed
no page: "i checked to see if it is working so i select printer secretprinter
and get supply info. it seems like it sends something to the printer so i have
been doing that and it is working now. so yes i sent supply info not a print
page but it worked." Which connections belong to which of his checks is not
shown; the clocks were not compared. The first read began at 20:47:28Z on
2026-10-27, before the reading of the build of `de1d171` at 20:52:29Z that day,
so as to take in that build's last hours too; its first line was at 21:02:35Z,
so no line was read twice. It grouped every line that is not a `Job:` line,
with addresses, the printer's host name, UUIDs, the pin and numbers masked,
counted the `Job:` lines, and listed each loss with how it ended. The second
printed the lines from 16:21:12Z with the same values hidden, IPv4 addresses
too. Both were given by Claude, tested first in Claude's workspace with
PowerShell 7.4.6 on a made-up log, and run by Edwin in his SSH session. The
arithmetic was done in Claude's workspace. Claude predicted, and it did not
hold: that the first line would be at 20:47:28Z; that no connection would fail;
that the printer would be off offer for about a tenth of the time (it was 6.5%
of the whole span read, 7.8% of this build's part); and that the check at about
12:57 pm Eastern would show a connection opened then that completed, besides
the three that failed (none did; the one that completed then had been opened at
16:52:10Z). Held: the time Windows started, the service running, the stop and
start lines of the swap, 4 to 16 losses (7, one of them the build of
`de1d171`'s), each ended by an answer, no `Could not reopen` line, one client
address in the first read, every connection located from the cache, no
connection of a million bytes or more, the three failures accepted at
16:57:21Z, and the checks after the first read logged after 16:57:31Z.)*

*(Status note, 2026-10-30, by Claude, Claude Opus 5.5: two more reads of the
service's log on FIOS-STB-01, read-only, each from where the one before ended:
at 18:17:29Z on 2026-10-29, from 17:08:48Z on 2026-10-28; and at 18:27:19Z on
2026-10-30, from 16:50:56Z on 2026-10-29, one second after the last line the
first had read. Nothing was run, restarted or changed for them. At both,
Windows had last started at 20:55:31Z on 2026-10-14 and the service was
running. The first read 172 lines, from 19:16:43Z to 16:50:55Z; the second 148,
from 22:20:12Z on 2026-10-29 to 16:59:43Z on 2026-10-30. Every line is
accounted for: 28 and 72 lines that are not `Job:` lines, and 36 and 19
connections of four `Job:` lines each. No line says `capped from`,
`Printer lookup failed` or `Could not reopen`, none reports a conflict, and the
service did not restart. **The service lost the printer 3 times in the first
read and 8 in the second, and found it again each time by one of its own
questions being answered; it heard the printer without being asked in none of
them.** Counted from the loss, each answer came 0 to 2 seconds after a question
due by the schedule under "The arithmetic" in the
[finding of 2026-10-22](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md):
the questions due 0 and 295 seconds after the losses at 19:16:43Z and 12:15:51Z
in the first read, and 556, 35, 162, 6, 22, 0, 93 and 0 seconds after the
losses at 22:20:12Z, 05:31:35Z, 12:39:08Z, 12:43:52Z, 12:46:00Z, 12:49:59Z,
12:53:43Z and 13:00:31Z in the second. The answer that ended the third loss of
the first read, at 16:48:52Z, is counted from elsewhere: at 16:50:52Z the
service logged `The printer-side interface is usable again`, so the
printer-side Wi-Fi had been seen not usable; its questions started over from
that line, and the printer answered at 16:50:53Z. When the Wi-Fi dropped is not
shown, because nothing is logged then. Off offer, counted as before: 7 minutes
6 seconds in the first read, 0.47% of the 25 hours 9 minutes it covers, and 15
minutes 6 seconds in the second, 0.98% of 25 hours 36 minutes; the longest
loss, from 22:20:12Z on 2026-10-29, took 9 minutes 21 seconds. Six of the
second read's eight losses fell between 12:39:08Z and 13:00:34Z on 2026-10-30,
5 minutes 6 seconds off offer in all. Two of them began 120 and 121 seconds, by
the log's whole seconds, after the answer that had ended the loss before, the
longest an answer is believed (`REQ-RES-013`): so in each the printer answered
one question and then nothing that was taken for those 120 seconds. Why is not
shown. Each loss was followed by one probe before the printer was offered
again, and each came back clear. In each read every connection, 36 and then 19,
came from one IPv6 link-local address; whether it was the same one in both is
not shown, as the reads hid it. Every one was located from the resolver's
cache, relayed over TLS 1.2 with the printer's certificate matching the pinned
fingerprint, to one printer endpoint, and completed. None failed, none was
refused, none carried a million bytes or more, and no TLS handshake failed to
complete. Edwin's reading of the losses, on 2026-10-29: "the losses i think are
the printer. when it is down, it is not answering on either network." **This
build's whole run, from 00:58:46Z on 2026-10-28 to the read at 18:27:19Z on
2026-10-30, 65 hours 28 minutes 33 seconds:** no restart; 17 losses, each ended
by an answer to one of its own questions; 1 hour 37 minutes 27 seconds off
offer, 2.5%; 18 probes, at startup and after each loss, all clear; and 86
connections accepted and none refused, of which 83 completed and 3 failed, the
three of 16:57:31Z on 2026-10-28 above. Claude predicted, and it did not hold:
the first read's first line at 17:08:48Z, the time of the read before it and
not of its last line (19:16:43Z); 8 to 12 losses and 5% to 11% off offer in the
first read (3, 0.47%), and no `usable again` line (one); 1 to 6 losses in the
second (8), and the second read at about 18:3xZ (18:27:19Z). Held: the time
Windows started, the service running, no restart, each loss ended by an answer
and none heard, every connection located from the cache, relayed over TLS 1.2
with the pin matching and completed, and none failing. Each read was one
read-only command given by Claude and run by Edwin in his SSH session, which
grouped every line that is not a `Job:` line with addresses, the printer's host
name, UUIDs, the pin and numbers masked, counted the `Job:` lines with every
failure and every connection of a million bytes or more, and listed each loss
with how it ended; the arithmetic was done in Claude's workspace. On
2026-10-29, at Edwin's direction, leftover builds and zips were removed from
his profile on both machines, each listed first and then removed by name: on
FIOS-STB-01, of the builds and zips there, all but the unpacked folders of this
build and of release 0.5.0; on the development machine all of them, this
build's and that of `de1d171` included. So "Left as they are" below no longer
holds: going back to 0.5.0 is the install command with its folder, and going
back to the build of `de1d171` would need a new build. This build is still
running.)*

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
  show how it fares. *(Status note, 2026-10-28, by Claude, Claude Opus 5.5:
  its first 16 hours were read; see the status note at the top.)* *(Status
  note, 2026-10-30, by Claude, Claude Opus 5.5: read to 65 hours 29 minutes;
  see the second status note at the top.)*
- **The last hours of the build of `de1d171`,** from the read at 20:52:29Z on
  2026-10-27 to its stop at 00:58:46Z on 2026-10-28, 4 hours 6 minutes, were
  not read. *(Status note, 2026-10-28, by Claude, Claude Opus 5.5: read on
  2026-10-28; see the status note at the top, and that day's on
  [that build's finding](2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md).)*

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
