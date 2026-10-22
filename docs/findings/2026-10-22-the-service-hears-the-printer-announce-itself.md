# The service hears the printer announce itself

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-22. Reviewed by a human before merge.*

**Status: built and measured against the code by tests; not run on a network,
and no release carries it. While the service holds the printer unreachable,
the printer's own mDNS records, heard on the printer-side interface without a
question of the service's waiting for them, now make it reachable as an answer
does (README `REQ-RES-011`): an announcement as the printer starts, or an
answer it multicasts to another device. They are taken only from a response
sent to the mDNS group from UDP port 5353, as RFC 6762 §6 requires, and only
when they hold the configured `_ipps._tcp` instance's SRV record and an
address record for the host it names, each within its TTL. A record with a TTL
of 0, a goodbye, is no longer taken as an answer by any lookup. Goodbyes still
do not withdraw the printer, and nothing changes while it is held reachable.
Edwin West decided this on 2026-10-22, after the printer was seen the day
before to announce itself eight times in the two minutes after it was switched
on, while the service kept what arrived only for a question that was waiting.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name. The addresses in the tests are made up or from the
documentation ranges; the printer's host name in them is redacted to
`EPSON000000`, as in
[the audit of 2026-09-04](2026-09-04-pre-publication-audit.md).

## Why this was done

[The finding of 2026-10-21](2026-10-21-the-printer-announces-itself-when-it-starts.md)
recorded a test: with a receive-only listener on the printer-side network,
Edwin restarted the printer. It said goodbye when it was switched off, and
when it came back it probed for its host name and announced itself eight
times in 2 minutes 7 seconds, the gap doubling from 1 second to 64, as RFC
6762 §8.1, §8.3 and §10.1 describe. The service found it 9 seconds after
losing it; worked out from the code and the listener's times, that was because
one of its questions happened to be waiting when the second announcement
arrived: `PrinterResolver` kept a datagram only for a lookup that was waiting,
and dropped the rest. After a long outage its
questions are an hour apart (`REQ-RES-008`), and on 0.4.0's first day it found
the printer 50 to 60 minutes after Edwin restarted it and its port answered.
Starting the questions over when the adapter comes back (`REQ-RES-010`) does
nothing when the printer itself is what went away.

## Decided

After that finding was committed, Claude put one question to Edwin, with three
options:

- **"Hear announcements (Recommended)"**: while the printer is unreachable, its
  announcement makes it reachable again within seconds; an already expired
  goodbye no longer counts as an answer; goodbyes themselves are still
  ignored; a device on the printer network could fake an announcement at any
  time, not only when the service asks, and jobs would still fail at the
  certificate pin.
- **"Announcements and goodbyes"**: the same, and a goodbye withdraws the
  printer about a second later instead of about 35; but a device on the
  printer network could then withdraw it at will, briefly, and whether this
  printer's goodbye names the records the service uses is not shown.
- **"Leave it as it is"**: no code change.

Edwin chose "Hear announcements (Recommended)". Before writing it, Claude told
him the design in bullets, including the two rules of RFC 6762 §6 below, which
it had read that day.

## What RFC 6762 says

Read on 2026-10-22 through a fetch tool, from
`https://www.rfc-editor.org/rfc/rfc6762` (§11 from
`https://www.rfc-editor.org/rfc/rfc6762.txt`). The tool returns at most about
125 characters of quotation at a time; each sentence below was given in
pieces, confirmed by the tool to be contiguous, and joined here with the RFC's
line breaks as spaces. §8.1, §8.3 and §10.1 are quoted in the finding of
2026-10-21.

§6, Responding:

> Multicast DNS queriers receiving Multicast DNS responses do not care what
> question elicited the response; they care only that the information in the
> response is true and accurate.

> A Multicast DNS querier MUST only accept unicast responses if they answer a
> recently sent query (e.g., sent within the last two seconds) that explicitly
> requested unicast responses. A Multicast DNS querier MUST silently ignore all
> other unicast responses.

> The source UDP port in all Multicast DNS responses MUST be 5353 (the
> well-known port assigned to mDNS). Multicast DNS implementations MUST
> silently ignore any Multicast DNS responses they receive where the source UDP
> port is not 5353.

§7.1, Known-Answer Suppression:

> A Multicast DNS querier MUST NOT cache resource records observed in the
> Known-Answer Section of other Multicast DNS queries.

§11, Source Address Check:

> All responses received with a destination address in the IP header that is
> the mDNS IPv4 link-local multicast address 224.0.0.251 or the mDNS IPv6
> link-local multicast address FF02::FB are necessarily deemed to have
> originated on the local link, regardless of source IP address.

So a querier is meant to use a multicast response whatever question it
answered, and the service now does, for the one instance it serves. Nothing it
hears this way answers a query of its own, so by the second quotation every
unicast response is ignored, and by the third every response from another
port.

## Telling a multicast from a unicast

The service's socket already asked Windows for the arrival interface of each
datagram with `IP_PKTINFO` (`REQ-ADV-015`), and threw away the other half of
what that gives: the destination. Microsoft documents `IN_PKTINFO`'s
`ipi_addr`, on receive, as "The destination IPv4 address from the IP header of
the received packet when used with the LPFN_WSARECVMSG (WSARecvMsg) function",
and `IN6_PKTINFO`'s `ipi6_addr` in the same words for IPv6 (Microsoft Learn,
read 2026-10-22). In the .NET runtime at tag `v10.0.0`,
`SocketPal.Windows.cs` builds `IPPacketInformation.Address` from that field,
and `pal_networking.c` from `ipi_addr` on Linux. `MdnsDatagram` now carries it
as `Destination`, and `SentToGroup` says whether it is 224.0.0.251 or ff02::fb,
the IPv6 group compared without its scope. A destination not known, as in a
datagram a test builds without one, is not the group.

A test sends a probe to 224.0.0.251 from a second socket, with a multicast TTL
of 0 so it stays on the machine, and reads it back: its destination is the
group. It ran in Claude's workspace, on Linux; on Windows it runs at the
measure before the commit.

## What the service does now

- **`PrinterAnnouncements`, new** (`src/SecretPrinter.Resolution`). It holds
  what the printer says about the configured instance and its host, and
  reports the printer heard when a datagram brings the instance's SRV record
  or an address record for the host that SRV names, and the other is already
  held and still valid. It takes a datagram only if it arrived on the
  printer-side interface, was sent to the mDNS group, came from port 5353 and
  is a response, and it reads only the Answer and Additional sections, class
  IN. Each record is held with its TTL counted from when it was heard. A record
  with a TTL of 0 is never held, and forgets the same record if one is. The
  answer's lifetime is the shorter of the two left; less than a second left is
  no answer. At most 16 address records are held: those whose TTL has run out
  go first, then the one heard longest ago.
- **Why records are held across datagrams:** the printer sent each
  announcement on 2026-10-21 as three datagrams, the first beginning with its
  address record and the SRV in a later one. A rule that needed both in one
  datagram would not have heard it.
- **`PrinterResolver`** reads its socket as before. A datagram that arrives
  while a lookup is waiting goes to that lookup, as before; one that arrives
  while none is, which it dropped, now goes to the `PrinterAnnouncements` it
  was given. A fault while handing it over stops the reader, and every lookup
  after says why, as for any other fault of the reader. And a lookup no longer
  takes an SRV or address record whose TTL is 0.
- **`PrinterReachability.RecordAnnouncement`**: while the printer is held
  unreachable, what was heard is taken as an answer, unless it is no newer
  than the answer held or its lifetime is already over; while it is held
  reachable, nothing changes.
- **`PrinterWatch.RecordAnnouncement`** queues it, as a job's answer is queued,
  and wakes the watch's loop. On the loop, a printer held unreachable becomes
  reachable at once, without a question, and is offered as after any answer:
  the names probed for, the advertisement restored. It is logged as
  information: `Printer reachable again: it was heard on the printer-side
  interface without being asked (an announcement, or its answer to another
  device).`
- **`PrinterSide`** passes one `PrinterAnnouncements` to every resolver it
  makes, so it still hears after the socket is reopened (`REQ-RES-009`), and
  records heard just before a reopen are still held after it.
- **`ServiceHost.RunAsync`** makes the `PrinterAnnouncements` for the
  printer's `_ipps._tcp` instance before the printer side, and tells it the
  watch once the watch exists; until the printer has answered at startup, what
  is heard has nowhere to go and is dropped, as before.

Not changed: the questions and their schedule; the startup wait, which keeps
its own sequence; anything the service sends; the lookups a print job makes,
but for goodbyes; goodbyes as a reason to withdraw; anything while the printer
is held reachable.

## Security

Nothing new is sent, and nothing new is opened: the same socket, the same
parser, the printer-side interface only. What changes is when the service
believes what it hears there.

- **A device on the printer network** could already make the service offer the
  printer, by answering one of its questions with records of its own. It can
  now do so whenever the printer is held unreachable, not only while a question
  is waiting. A forged address sends print jobs where nothing answers, or to a
  device whose certificate does not match the pinned fingerprint, and the relay
  abandons them (`REQ-SEC-013`); the client sees a failed print, as it would
  with the printer away.
- **The same device could give a long TTL,** and the service would hold the
  printer reachable until 80% of it before asking again. That is true of an
  answer to a question too, before this change; no TTL is capped anywhere.
  Raised below.
- **Goodbyes are not acted on,** so nothing heard can make the service withdraw
  the printer.
- **Only multicast responses from port 5353** are taken, which is what RFC 6762
  §6 requires of a querier. The printer-side network is the one the service
  trusts for the printer's address in any case: `REQ-RES-006` already takes
  answers only from there.
- **Memory:** at most 16 address records, one SRV and one TXT are held, so a
  device sending records for many names cannot make it grow.

## The tests

Thirty-five new tests. The first thirty-three were written first and run
against stubs that compiled and did nothing: eighteen failed and fifteen
passed. Two were added after the change, before the mutations: one because
nothing tested that a response's Authority Section is ignored, and one so that
a fault while hearing would be shown to stop the reader loudly. One was
extended after a mutation (below). After everything else was done, the stubs
were put back once more, with all thirty-five, and the table gives that run:

| Test | Against the stubs |
| --- | --- |
| The printer is heard from one response holding its instance's SRV and its host's address | failed |
| An announcement in three packets, the address first and the SRV later, is heard when the SRV arrives | failed |
| An SRV heard before its address is completed when the address arrives | failed |
| The lifetime reported is the shorter of the two records' lifetimes left, counted from now | failed |
| Each response that brings the records again reports the printer again; the caller decides what that means | failed |
| A response sent by unicast is ignored, as RFC 6762 s6 says of unsolicited unicast responses | passed |
| A datagram whose destination is not known is ignored | passed |
| A response from a port other than 5353 is ignored, as RFC 6762 s6 says | passed |
| A response that arrived on another interface, or on none configured, is ignored | passed |
| A question carrying the same records, as known answers or as a probe's proposal, is not the printer's word | passed |
| Records in a response's Authority Section are not taken | passed |
| A record of a class other than IN is not taken | passed |
| An address for another host than the SRV names does not complete it | passed |
| Traffic about other devices, after the printer was heard, reports nothing more | failed |
| A record with a TTL of 0 is a goodbye and is never taken | passed |
| A goodbye forgets the address it names, so a later SRV does not complete it | passed |
| A goodbye forgets the SRV it names, so a later address does not complete it | passed |
| A goodbye for another address of the host leaves the address held | failed |
| A record whose TTL has run out is not used | passed |
| A lifetime of less than a second left is not reported | passed |
| At most 16 addresses are held, and the one heard longest ago is forgotten first | failed |
| An address whose TTL has run out is forgotten before one still valid | failed |
| A response that arrives while no lookup is waiting is handed to the printer's announcements | failed |
| A response that arrives while a lookup is waiting goes to that lookup, not to the announcements | passed |
| A fault while hearing the printer between lookups stops the reader, and the next lookup says why | failed |
| A lookup does not take a goodbye, a record whose TTL is 0, as its answer | failed |
| A lookup answered only by a goodbye fails as unanswered | failed |
| The printer heard while it is held unreachable is reachable again, and the schedule runs from what was heard | failed |
| The printer heard while it is held reachable changes nothing | passed |
| Something heard whose lifetime is over, or no newer than the answer held, does not revive the printer | passed |
| The printer heard without being asked while it is held unreachable is offered again at once, without a question, and the log says how it was found | failed |
| The printer heard without being asked while it is held reachable changes nothing and says nothing | failed |
| Every resolver made here hands the printer's announcements on, after a reopen too | failed |
| A datagram sent to the mDNS group is told from one sent by unicast, or to an unknown destination | failed |
| An IPv4 arrival sent to the mDNS group records the group as its destination | failed |

The sixteen that pass against the stubs guard against doing too much; the
mutations below show each of them fails when its rule is broken. The watch's
test with the printer held reachable fails against the stubs because it waits
for the watch to wake, and a stub never wakes it.

After the change, in Claude's workspace: the Mdns suite 40 tests and 9
skipped, the resolution suite 74, the service suite 125 and 1 skipped, all
passing; the other four suites unchanged (28, 27, 62, 205). In their final
form the three changed suites were run 10 times in a row, and the
resolution and service suites 12 more times four at once, with the
same result every time.

**One test was itself wrong, and was corrected before the end:** the first
version of the test of a fault while hearing put a lookup to the resolver
while the reader might still be deciding where the datagram went, and the
lookup could take the datagram for itself. It failed once, in a mutation run
of an unrelated rule. It now starts the lookup only once the datagram has
reached the announcements.

## Breaking each claim on purpose

Forty mutations, each made alone, built, run against every suite that includes
the changed file, and undone. Two were first written in a form that did not
compile, and were written again. One, a question taken as the printer's word,
was not caught at first: the question test put the records only in a probe's
Authority Section, which is ignored anyway. The test was extended with a
question whose Answer Section lists them as known answers, as §7.1 describes,
and the mutation was then caught. Two were run again after the fault test was
corrected; the table gives the last run of each.

| Mutation | Caught by |
| --- | --- |
| Any interface taken | the interface test |
| Only a datagram with no interface refused | the interface test |
| Unicast taken | the unicast test; the unknown-destination test |
| Any source port taken | the port test |
| Questions taken | the question test |
| Any class taken | the class test |
| The Authority Section read | the Authority Section test |
| An SRV's goodbye not forgetting it | the SRV goodbye test |
| An SRV with TTL 0 held as if alive | **no test**; see below |
| An address's goodbye not forgetting it | the address goodbye test |
| An address with TTL 0 held as if alive | the goodbye-never-taken test; the other-address goodbye test |
| A goodbye for any address forgetting the one held | the other-address goodbye test |
| Any address completing it | the other-traffic test |
| The SRV not completing it | four tests |
| No cap on addresses held | the cap test |
| Expired addresses not forgotten first | the expired-first test |
| The newest address forgotten first | the cap test |
| Less than a second taken | the expired test; the under-a-second test |
| Records long run out taken | the same two |
| The longer lifetime reported | three tests |
| TXT not carried | the one-response test |
| Not handed over between lookups | two resolution tests; the printer-side test |
| Handed over during lookups too | the during-a-lookup test |
| A lookup taking a goodbye | both goodbye lookup tests |
| A fault while hearing swallowed | the fault test |
| Heard while reachable taken | the reachable test |
| Heard no newer than the answer held taken | the stale test |
| Heard after its lifetime taken | the stale test |
| Any IPv4 address taken as the group | the destination test; the unicast test |
| The IPv6 group not recognised | the destination test |
| The IPv6 group compared with its scope | the destination test |
| The destination not kept by the socket | the socket test |
| Heard not taken in on the watch's loop | both watch tests |
| The watch's wait not woken | both watch tests |
| The ordinary recovery line logged | the watch held unreachable |
| Heard not queued | both watch tests |
| The first resolver not given the announcements | the printer-side test |
| A reopened resolver not given them | the printer-side test |
| **The watch never told, in `ServiceHost.RunAsync`** | **no test** |
| **The printer side not given them, in `ServiceHost.RunAsync`** | **no test** |

- **An SRV with TTL 0 held as if alive** changes nothing that can be seen: it
  replaces the SRV held with one whose lifetime is already over, which is no
  answer, as forgetting it would be.
- **No test runs `ServiceHost.RunAsync`,** so nothing but a run shows that the
  service wires the announcements in.

## What a run on a network would need to show

- With the printer held unreachable and the questions already minutes apart,
  the printer switched off and on: the line `Printer reachable again: it was
  heard on the printer-side interface without being asked`, within seconds of
  the printer's first announcement, and the advertisement restored after it.
- While the printer is held reachable, no such line when it announces itself.
- A page printed through it afterwards.

## What this does not show

- **Any of it on a network, on Windows or in the release build,** apart from
  the socket test that runs at the measure on the development machine. The
  wiring in `ServiceHost.RunAsync` is run by no test.
- **That the printer announces itself after a long silence** like those of
  2026-10-20 and 2026-10-16, or when its own Wi-Fi reconnects; the test of
  2026-10-21 restarted it once, after about 40 seconds off.
- **That FIOS-STB-01 receives the printer's announcements between questions.**
  The test of 2026-10-21 heard them on the development machine; FIOS-STB-01
  took one only while a question waited.
- **How much shorter the losses will be.** That depends on whether the printer
  speaks when it comes back.

## Raised, not decided

- **The lookups themselves do not follow the two rules of RFC 6762 §6** quoted
  above: a lookup takes a unicast response, though none of its questions ask
  for one, and a response from any source port. Found while reading §6 for
  this change; not changed here, one thing at a time. Claude's to settle, as
  its own change.
- **No TTL is capped.** An answer, or now a record heard, with a very long TTL
  holds the printer reachable without a question until 80% of it. Whether to
  cap the lifetime the service believes is a question for later.
- **The startup wait does not use what is heard.** A service that starts while
  the printer is away waits for its own questions, as before.
- **Goodbyes,** Edwin's choice of 2026-10-22: heard and not acted on.

## How it was measured

- **The tests,** in Claude's workspace, on Linux, with .NET SDK 10.0.113 and
  runtime 10.0.13. The six test projects that build directly were built and
  run. The service and its test project were built from the repository's own
  files by two scratch projects outside the repository, which take
  `System.ServiceProcess.ServiceController` from the SDK's own folder because
  NuGet cannot be reached; that is not the build a release makes.
- **The stubs and the mutations** were applied to the working files by
  scratch scripts that checked each text replaced occurred once, and restored
  the files afterwards; each restored file was compared with its saved copy.
- **SpecCheck,** with the scratch service build added: 108 requirements, 23
  assemblies, 18 evidence rows, 578 test records, 90 requirements with a
  passing test, 89 covered by code, and the same 2 binding gaps as before this
  change, `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage
  matrix gains one row, `REQ-RES-011`, `OK`, and the `REQ-RES-006` row names a
  new test first; no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.
- **RFC 6762, Microsoft Learn and the .NET runtime's source** were read on
  2026-10-22 as described above.

## What changed

- `src/SecretPrinter.Resolution/PrinterAnnouncements.cs`, new.
- `src/SecretPrinter.Resolution/PrinterResolver.cs`: the announcements given
  what arrives between lookups; goodbyes not taken by a lookup; a section in
  the header.
- `src/SecretPrinter.Resolution/PrinterReachability.cs`: `RecordAnnouncement`;
  the purpose and a marker's text widened.
- `src/SecretPrinter.Mdns/MdnsSocket.cs`: `MdnsDatagram.Destination` and
  `SentToGroup`; `ReceiveAsync` keeps the destination.
- `src/SecretPrinter.Service/PrinterWatch.cs`: `RecordAnnouncement`, and the
  loop and its wait taking it in.
- `src/SecretPrinter.Service/PrinterSide.cs`: the announcements passed to every
  resolver.
- `src/SecretPrinter.Service/ServiceHost.cs`: the wiring, and one comment.
- `src/SecretPrinter.Service/AdapterWatch.cs`: one comment.
- Tests: the new `PrinterAnnouncementsTests.cs` (22) and its registration;
  five in `PrinterResolverTests.cs`, three in `PrinterReachabilityTests.cs`,
  two in `MdnsSocketTests.cs`, two in `PrinterWatchTests.cs`, one in
  `PrinterSideTests.cs`.
- `README.md`: a new row, `REQ-RES-011`; notes on `REQ-RES-008`, `REQ-RES-010`
  and `REQ-LIF-006`; the Known problems entry on the printer-side network and
  the section's note.
- `docs/operating.md`: "Coming back can be slow", and a header note.
- Status notes on the findings of
  [2026-10-21](2026-10-21-the-printer-announces-itself-when-it-starts.md),
  [2026-10-03](2026-10-03-the-printer-came-back-at-an-hourly-question.md) and
  [2026-10-10](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md).
