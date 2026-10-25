# The printer announces itself when it starts

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-22, from a test made on 2026-10-21. Reviewed by a human before
merge.*

**Status: measured, once, on 2026-10-21. A receive-only tool listened on the
printer-side network from the development machine while Edwin West restarted
the printer. When the printer was switched off it sent a goodbye: one packet
of 16 records, each with a TTL of 0. When it came back, about 40 seconds
later, it sent three probes for its host name, then announced itself eight
times in 2 minutes 7 seconds, the gap doubling each time from 1 second to 64,
as RFC 6762 §8.1, §8.3 and §10.1 describe. The service on FIOS-STB-01 used
neither. Its resolver keeps what arrives from the printer only while one of
its own questions is waiting for an answer, and drops the rest. It went on
offering the printer for about 35 seconds after the goodbye, until its record
of the printer ran out, and found the printer again 9 seconds after that,
because one of its questions happened to be waiting when the second
announcement arrived. After a long outage its questions are up to an hour
apart, and announcements that are over within about two minutes would not be
heard. The test changed nothing in the repository, and no code changed.
Whether the service should listen for the printer's own announcements and
goodbyes is raised below, not decided.**

*(Status note, 2026-10-22, by Claude, Claude Opus 5.5: decided and built the
same day. Edwin West chose that the service should hear the printer's
announcements and not act on its goodbyes. While the service holds the printer
unreachable, the printer's own records heard on the printer-side interface,
sent to the mDNS group from port 5353, now make it reachable as an answer does
(README `REQ-RES-011`), and a lookup no longer takes a record whose TTL is 0.
Measured against the code by tests; not run on a network, and no release
carries it. The two rules of RFC 6762 §6 that a querier ignores unicast
responses it did not ask for and responses from other ports are applied to
what is heard; the lookups themselves do not yet apply them. See [the finding
of 2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md).)*

*(Status note, later on 2026-10-22, by Claude, Claude Opus 5.5: run on a
network that evening, once, on FIOS-STB-01 in a build from source. After the
printer had been switched off for about 16 minutes, the service heard it and
offered it again no more than about 40 seconds after it was switched on,
without waiting for its next question. No release carries it. See [the finding
of the run](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md).)*

*(Status note, 2026-10-25, by Claude, Claude Opus 5.5: release 0.5.0, published
on 2026-10-25 and installed on FIOS-STB-01 that day, is the first to carry
`REQ-RES-011`. In the three days before, the build of `5a291c4` lost the
printer 31 more times after the run and heard it in none of them. See [the
finding of the
release](2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md)
and the status note of 2026-10-25 on [the finding of the
run](2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name. The tool's output names every device that speaks mDNS on
that network; only the printer's packets, and the questions naming it, were
pasted, with addresses and the host name replaced by labels.

**About the times.** Times ending in `Z` are UTC. The listener's are from the
development machine's clock, to a tenth of a second; the service's are from
FIOS-STB-01's clock, to the second. **The two clocks were not compared**, so
any interval between a listener time and a service time is approximate. Times
in am or pm are US Eastern, UTC-4. Everything here happened on 2026-10-21.

## Why this was tested

When the service loses the printer it asks again after 1, 2, 4… seconds, up to
once an hour (`REQ-RES-008`). Since 2026-10-10 it starts that over when
Windows reports the printer-side adapter usable again (`REQ-RES-010`), which
does not help when the printer itself is what went away:

- On 2026-10-03 the service found the printer at a question 129 minutes after
  losing it; the printer had begun answering at some time in the hour before
  ([finding](2026-10-03-the-printer-came-back-at-an-hourly-question.md)).
- On 0.4.0's first day the printer's port failed all 31 tests of Edwin's
  script `Check-SP.ps1` from 7:10 pm on 2026-10-20 to 12:10 am. Edwin
  restarted the printer at about 12:15 am (about 04:15Z), by his recollection;
  before that, in his words, it "was not answering on either network". Its
  port answered at the script's run begun at 04:20:28Z, and the service found
  it at 05:10:36Z, 50 to 60 minutes after the port answered
  ([status note of 2026-10-21](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md)).
- Not every such loss is long. In a loss of 10 hours 14 minutes on 2026-10-16,
  during which Edwin restarted the printer at a time not recorded, the service
  found it no more than about ten minutes after its port answered
  ([finding](2026-10-18-the-third-release-ran-three-days-on-fios-stb-01.md)).

The question was whether the printer tells the network that it is back,
without being asked. RFC 6762 says a responder does. The finding of 2026-10-03
records that Claude put to Edwin, as the third of four courses, to "take the
printer's own multicast records, heard while it is held unreachable, as an
answer". He chose then to keep the schedule as it was, and on 2026-10-10 chose
the second course (`REQ-RES-010`). No earlier finding records the printer
announcing itself.

## What RFC 6762 says

Read on 2026-10-22 through a fetch tool, from
`https://www.rfc-editor.org/rfc/rfc6762`. The tool returns at most about 125
characters of quotation at a time; each sentence below was given in pieces,
said by the tool to be contiguous, and joined here with the RFC's line breaks
as spaces.

§8.1, Probing:

> The first startup step is that, for all those resource records that a
> Multicast DNS responder desires to be unique on the local link, it MUST send
> a Multicast DNS query asking for those resource records, to see if any of
> them are already in use.

> 250 ms after the first query, the host should send a second; then, 250 ms
> after that, a third.

> All probe queries SHOULD be done using the desired resource record name and
> class (usually class 1, "Internet"), and query type "ANY" (255), to elicit
> answers for all types of records with that name.

§8.3, Announcing:

> The second startup step is that the Multicast DNS responder MUST send an
> unsolicited Multicast DNS response containing, in the Answer Section, all of
> its newly registered resource records (both shared records, and unique
> records that have completed the probing step).

> The Multicast DNS responder MUST send at least two unsolicited responses,
> one second apart. To provide increased robustness against packet loss, a
> responder MAY send up to eight unsolicited responses, provided that the
> interval between unsolicited responses increases by at least a factor of two
> with every response sent.

§10.1, Goodbye Packets:

> In the case where a host knows that certain resource record data is about to
> become invalid (for example, when the host is undergoing a clean shutdown),
> the host SHOULD send an unsolicited Multicast DNS response packet, giving the
> same resource record name, rrtype, rrclass, and rdata, but an RR TTL of zero.

> Queriers receiving a Multicast DNS response with a TTL of zero SHOULD NOT
> immediately delete the record from the cache, but instead record a TTL of 1
> and then delete the record one second later.

## Why the service does not hear them

`src/SecretPrinter.Resolution/PrinterResolver.cs` reads the printer-side
socket all the time and hands each datagram on only if a lookup is waiting:

```csharp
            // Kept only if a lookup is waiting now. Otherwise it arrived between
            // lookups, is not a reply to anything we asked, and is dropped.
            Volatile.Read(ref _listening)?.Writer.TryWrite(datagram);
```

The file's header says why, under "On reading the transport": a resolver that
read only while looking up "could find replies waiting that arrived long
before - the printer's answers to other devices' queries, for instance - and
take the first one naming our instance as the answer, with its TTL counted
from now. That is a remembered address by another route." So the dropping is
by design, and it was written against a stale answer, not against an
announcement heard as it arrives. Only an answer to a lookup makes the
printer reachable (`REQ-RES-008`; `REQ-RES-010`: "only an answer does").

## How it was read

- **The development machine, not FIOS-STB-01.** Its Wi-Fi adapter was
  connected to the printer-side network for the test (it had been
  disconnected); Windows put it in the Private category, with one address on
  that network. FIOS-STB-01 was not touched: it ran release 0.4.0 as installed
  on 2026-10-20, and its log was read afterwards, read-only, at 22:43:56Z.
- **The tool:** `tools/SecretPrinter.Listen`, the Release build made on the
  development machine at that day's measure. It binds UDP port 5353 shared,
  joins `224.0.0.251` on the address it is given, and prints one line for each
  packet; it never transmits (its README). It ran from Edwin's normal window
  with `--interface` set to the Wi-Fi's address on the printer-side network and
  `--duration 900`, its output to the screen and to a file in his temporary
  folder. It started at 22:00:25Z and in 900 seconds read 247 packets: 82
  queries, 165 responses, 0 unparseable.
- **A temporary firewall rule.** Before the run, the firewall rules whose
  name holds "mDNS" were listed in an elevated window: each named a program
  other than the tool (Windows' own name `svchost.exe`). Rules under other
  names were not listed.
  So that the tool could receive, a rule was added in that window, as narrow as
  Claude could make it: inbound, allow, UDP, local port 5353, the Private
  profile only, and that one program, named `SecretPrinter Listen test (remove
  after)`. It was read back after it was made, and removed at about 22:42Z, the
  removal printing `No MSFT_NetFirewallRule objects found with property
  'DisplayName' equal to 'SecretPrinter Listen test (remove after)'.` Whether
  the tool would have received anything without the rule was not tried. Lines
  of earlier output pasted into the elevated window with the command also ran
  there as commands; each failed, printed a usage message or was left
  unfinished, as their output showed.
- **The restart:** Edwin, after the run: "restarted printer around 22:01z and
  it came back within 1 minute". How he switched it off and on was not
  recorded.
- **What was pasted:** a PowerShell command read the tool's file on the
  development machine and printed only the packets from devices that had sent
  a reply naming the printer (one device: the printer) and the questions that
  named the printer from any device (only FIOS-STB-01's appeared), with
  addresses and the printer's host name replaced by labels, and the summary's
  counts.

## What the listener heard

Every repeat had the same sizes and TTL lists as the first of its kind. The
tool prints the TTL of every record in a reply's answer section, then as many
of the records' names as fit, and cuts the rest.

| Time (development machine) | From | What |
| --- | --- | --- |
| 22:00:59.3 | the printer | **The goodbye.** One reply, 708 bytes, 16 records, every TTL 0. First names: the PTR records of `_printer._tcp`, `_pdl-datastream._tcp`, `_http._tcp` and one beginning `_sca`; the rest cut by the tool. |
| 22:01:10.6, 18.2, 23.3, 28.3 | FIOS-STB-01 | A question for the SRV and TXT records of `EPSON ET-3760 Series._ipps._tcp.local`, the instance the service is configured with. Not answered. |
| 22:01:34.2 | FIOS-STB-01 | The same question. Not answered. |
| 22:01:38.9, 39.1, 39.4 | the printer | **Three probes,** each 655 bytes, questions of type ANY for its host name. |
| 22:01:39.6 to 39.7 | the printer | **Announcement 1:** three replies, of 1,427, 1,391 and 1,116 bytes, TTLs 120 and 4,500. The first begins with the IPv4 and IPv6 address records of its host name and reverse-lookup PTRs; the second with the SRV and TXT of its `_ipp._tcp` instance, then PTRs; the third with the TXT of another of its instances and the PTR `_services._dns-sd._udp.local`. |
| 22:01:40.5 | FIOS-STB-01 | The SRV and TXT question again. |
| 22:01:40.6 to 40.7 | the printer | **Announcement 2,** the same three replies. |
| 22:01:40.7 | FIOS-STB-01 | A question for the address record of the printer's host name. |
| 22:01:42.7 | the printer | **Announcement 3.** |
| 22:01:46.7 | the printer | **Announcement 4.** |
| 22:01:54.7 | the printer | **Announcement 5.** |
| 22:02:10.7 | the printer | **Announcement 6.** |
| 22:02:43.0 | the printer | **Announcement 7.** |
| 22:03:20.6 | FIOS-STB-01, then the printer | The SRV and TXT question, answered in the same tenth of a second by a reply of 693 bytes, TTLs 120 and 4,500. |
| 22:03:46.9 | the printer | **Announcement 8.** |
| 22:04:58.4 to 22:14:44.9 | FIOS-STB-01, then the printer | The same question and reply seven more times, 96.7 to 98.7 seconds apart, each answered within 0.3 seconds. |

- **The gaps between announcements** were 1.0, 2.1, 4.0, 8.0, 16.0, 32.3 and
  63.9 seconds: doubling, eight in all, the most §8.3 allows. The eighth came
  2 minutes 7.3 seconds after the first.
- **The printer was off for about 40 seconds:** 39.6 seconds from the goodbye
  to the first probe.
- **FIOS-STB-01's four unanswered questions before 22:01:34** fit the
  reconfirmations of `REQ-RES-008`, at 80 to 97% of a record that lives 120
  seconds; its questions after the printer was back, about 97 seconds apart,
  fit the same schedule against the printer's answers.
- **Which of the three replies of an announcement carried the SRV and TXT of
  the `_ipps._tcp` instance, and whether the goodbye named them or the host's
  address,** is not shown: the tool cut each list of names.

## What the service logged

From FIOS-STB-01's log, read at 22:43:56Z: the service `Running`, and nine lines
from 21:55Z.

| Time (FIOS-STB-01) | Lines, shortened |
| --- | --- |
| 22:01:34 | `Printer unreachable …`; `No longer accepting print jobs on Ethernet.`; `Advertisement withdrawn …` |
| 22:01:43 | `Printer reachable again …`; `Probing for the advertised names before offering them (RFC 6762 s8.1).`; `Probe clear …`; `Accepting print jobs on …:631 (Ethernet); …` for its IPv4 address and for its link-local address |
| 22:01:45 | `Advertisement restored …` |

The printer was not offered for 11 seconds. Nobody printed.

## What this shows

1. **This printer says goodbye when it is switched off, and probes and
   announces itself when it starts,** as RFC 6762 describes, with the most
   announcements §8.3 allows. Seen once, on one restart of about 40 seconds.
2. **The service did not use the goodbye.** No question from FIOS-STB-01 was
   seen between the listener's start and 22:01:10.6, so none was waiting when
   the goodbye arrived, and by the code it was dropped. The service went on
   asking, and held the printer reachable until 22:01:34Z, when its record had
   run out with no answer: about 35 seconds after the goodbye, by clocks not
   compared. That is the time README Known problems describes: "For a short
   time after the printer stops answering, it is still offered, and a print in
   that time fails."
3. **The service found the printer by a question that happened to be
   waiting.** Worked out from the code and the listener's times, with the
   clocks not compared, and not shown: the first question after the loss, at
   22:01:34.2, waited out the 5-second resolve timeout (the default; the value
   set on FIOS-STB-01 was not read in this test) to about 22:01:39.2, and the
   first announcement came at 22:01:39.6, after it ended. The next question,
   at 22:01:40.5, was waiting when the second announcement came at
   22:01:40.6. At 22:01:40.7 FIOS-STB-01 asked for the printer's address,
   which the resolver does when it holds the instance's SRV record and not yet
   the address the SRV names. After that the listener heard nothing from the
   printer until the third announcement, at 22:01:42.7, whose first record is
   that address, and the service logged `Printer reachable again` at
   22:01:43Z. That is also the only sign that FIOS-STB-01 receives the
   printer's announcements at all.
4. **After a long outage the announcements would be missed.** They were over
   within 2 minutes 7 seconds of the first. Once the service's questions are
   an hour apart, each waits the 5-second resolve timeout, so a question is
   waiting for about 5 seconds of each hour, and by the code an announcement
   is kept only if it falls in those seconds. Worked out, not run.
5. **Later announcements did not move the service's next question.** The
   question at 22:03:20.6 fits 80 to 82% of 120 seconds after the answer of
   about 22:01:42.7, and not after any of the four announcements between.
   That fits the code, which drops them; that they carried the records the
   service uses is not shown, though each had the sizes and TTLs of the
   second.

## What this does not show

- **That FIOS-STB-01 hears the announcements between questions:** they were
  heard on the development machine, on the same network. Point 3 is the only
  sign from FIOS-STB-01, and it was a question waiting, not an announcement
  heard between questions.
- **Whether the printer announces itself after a restart that follows a long
  silence,** like the restart of about 04:15Z. Whether it announced itself
  then, and so whether the service could have found it within minutes of that
  restart rather than 50 to 60 minutes after its port answered, is in neither
  log.
- **Whether it says goodbye when its power is cut** rather than switched off,
  or when it stops answering by itself, as it did on 2026-10-20.
- **Whether it announces itself when its own Wi-Fi reconnects,** without a
  restart.
- **What the goodbye and the announcements held** beyond the names the tool
  printed (see above).
- **How far apart the two clocks were.**

## Raised, not decided

**Whether the service should listen for the printer's own records.** The
change would be: on the printer-side interface only, hear the printer's
unsolicited records while the service holds it unreachable, and take a
matching set (the configured instance's SRV record, and the address record of
the host it names) as an answer; and perhaps hear a goodbye for those records while it holds the
printer reachable, and take it as the printer leaving. What it would touch:

- `PrinterResolver`, whose reader drops what arrives between lookups (above),
  and the reason in its header;
- `PrinterReachability` and `PrinterWatch`, where only an answer to a lookup
  makes the printer reachable;
- README `REQ-RES-008` (the state "derived from mDNS resolution on the
  printer-side interface - the same query path a relayed job uses"),
  `REQ-RES-010` and `REQ-LIF-006`, and probably a new `REQ-RES-` row;
- `docs/operating.md`, "Coming back can be slow";
- security: only datagrams that arrive on the printer-side interface, read by
  the same parser. A device on the printer network can already answer the
  service's questions for the printer. Taking unsolicited records would let
  such a device be believed at any moment, not only while a question waits,
  and taking goodbyes would let it make the service withdraw the printer,
  which answering a question cannot do now. Print jobs stay protected by the
  pinned certificate (`REQ-SEC-013`). This has to be weighed, and RFC 6762
  read on it, before the question is put;
- and a run on hardware.

It changes what the service does and a requirement, so it is Edwin's to
decide.

**What the resolver does with a goodbye that arrives while a question is
waiting.** Read from the code, not tested: `ReceiveAnswerAsync` takes an SRV
record naming the instance, and an address record for its target, whatever
their TTL, so a goodbye holding both would be taken as an answer whose
lifetime is already over. What the service would then do has not been worked
out, and no test covers it. It belongs with the question above.

## What Claude said that was wrong, and what it predicted and did not

- **Two firewall readings ran in Edwin's normal window,** where Windows refused
  them, and the command hid the refusal (`-ErrorAction SilentlyContinue`). From
  their empty output Claude told Edwin that no rule covered the tool, and that
  Windows' own mDNS rule was absent or disabled, or the filter had missed it.
  The listing in an elevated window showed Windows' own rules present and
  enabled. Claude corrected this to Edwin in the next message. A reading whose
  conclusion is that something is absent must not hide its errors.
- **The service's times:** Claude predicted the loss at about 22:01:39 and the
  printer found at about 22:01:40. The log says 22:01:34 and 22:01:43; point
  3 above is how those times fit the code.

## Changed with this finding

- README, Known problems, the entry "When the printer-side network drops…":
  two sentences on the announcements and a dated note; the section's note,
  which had not recorded the sentences added to that entry on 2026-10-20,
  records them and this one.
- Status notes on the findings of
  [2026-10-03](2026-10-03-the-printer-came-back-at-an-hourly-question.md) (the
  third course),
  [2026-10-10](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md)
  (what the start over cannot do) and
  [2026-10-20](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md)
  (the 50 to 60 minutes).
