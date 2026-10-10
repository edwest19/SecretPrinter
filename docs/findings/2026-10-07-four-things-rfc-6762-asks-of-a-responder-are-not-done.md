# Four things RFC 6762 asks of a responder are not done

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-07. Reviewed by a human before merge.*

**Status: recorded, not corrected. The service's mDNS responder does not do
four things that RFC 6762 asks of one. Two are requirements of the standard and
two are recommendations. Each was read in the code and then measured against
the code on 2026-10-07, in Claude's workspace, with the test kit's fake
transport in place of a network. Two of the four had been seen on a network on
2026-10-02 and were recorded then as things "also seen". Until today the README
stated none of them. It now lists all four under "Known problems". No code
changed. Whether they are built before the first release is not decided.**

*(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5:
decided. Edwin West chose that release 0.1.0 ships with the four stated as
known problems and that they are built afterwards. See "Not decided" at the
end.)*

*(Status note, 2026-10-10, by Claude, Claude Opus 5.5: item 1 is built. The
responder now reads a query's Answer section and leaves out of its answer a
record the query already carries with at least half its TTL, as README
`REQ-ADV-026` states; see
[the finding of 2026-10-10](2026-10-10-a-question-that-carries-its-answer-is-no-longer-answered.md).
It is measured against the code by tests and has not yet been run on a network.
Items 2, 3 and 4 are not built. The order chosen is item 2, then item 4, then
item 3: item 2 keeps the send times that item 4's choice depends on, and item 3
changes the timing of every answer to a browse, so it comes last.)*

This is not a review of the service against the whole of RFC 6762. See "What
this does not show".

## The four

The sentences quoted are from
[RFC 6762](https://www.rfc-editor.org/rfc/rfc6762), read on 2026-10-07 through
a tool that fetches a page and returns the passages asked for. They were not
compared with the RFC by hand.

### 1. A question that already carries the answer is answered anyway (§7.1)

> A Multicast DNS responder MUST NOT answer a Multicast DNS query if the answer
> it would give is already included in the Answer Section with an RR TTL at
> least half the correct value.

- **The code.** `MdnsResponder.HandleAsync` reads a query's questions. It never
  reads the query's Answer Section.
- **Measured.** A query for `_universal._sub._ipp._tcp.local`, type `PTR`,
  carrying the service's own `PTR` record in its Answer Section with the full
  TTL of 4,500 seconds, was parsed as a query with one question and one known
  answer. The responder answered it: one multicast response with that `PTR`.
- **On a network.**
  [The finding of 2026-10-02](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md),
  item 2 under "Also seen": eight of an iPhone's questions in one run carried
  the service's `PTR` as a known answer with nearly its full TTL and were each
  answered, once more in a second run and eleven times in a third.

### 2. The same record can be multicast more than once in a second (§6)

> To protect the network against excessive packet flooding due to software bugs
> or malicious attack, a Multicast DNS responder MUST NOT (except in the one
> special case of answering probe queries) multicast a record on a given
> interface until at least one second has elapsed since the last time that
> record was multicast on that particular interface.

- **The code.** The responder keeps no record of when it last sent anything.
- **Measured.** The same question, put five times one after another, drew five
  multicast responses carrying the same record. All five were sent within one
  millisecond of each other.
- **On a network.** The same finding, item 3: three of the service's ten
  answers in one run followed the one before by 0.22, 0.34 and 0.67 seconds,
  repeating the same records.

### 3. An answer for a shared name is not delayed (§6)

> In any case where there may be multiple responses, such as queries where the
> answer is a member of a shared resource record set, each responder SHOULD
> delay its response by a random amount of time selected with uniform random
> distribution in the range 20-120 ms.

- **The code.** `HandleAsync` builds the response and sends it. Nothing waits.
  The service-type names it answers for, `_ipp._tcp.local` and
  `_universal._sub._ipp._tcp.local`, are shared: any printer on the client
  network answers for them too.
- **Measured.** In the run above the first response was sent 2.85 milliseconds
  after the program began timing, and the rest in the following millisecond.
- **On a network.**
  [The finding of 2026-09-29](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md)
  records the service's answer to a browse for printers arriving "about a
  millisecond later".

The RFC asks the opposite for names a responder holds alone: it "SHOULD NOT
impose any random delay" and should answer within 10 milliseconds. The service's
host name and instance name are such names, and it answers for them at once,
which is what is asked.

### 4. A question that asks for a unicast answer is answered by multicast (§5.4)

> When a question is received with the unicast-response bit set, a responder
> SHOULD usually respond with a unicast packet directed back to the querier.
> However, if the responder has not multicast that record recently (within one
> quarter of its TTL), then the responder SHOULD instead multicast the response
> so as to keep all the peer caches up to date, and to permit passive conflict
> detection.

- **The code.** The responder reads the unicast-response bit only to set it
  aside when it reads the question's class (`REQ-ADV-025`). It sends by unicast
  only to a legacy querier, one whose source port is not 5353 (`REQ-ADV-017`).
- **Measured.** A question with the bit set, from port 5353, was answered by
  multicast.
- **On a network.** Not looked for.

Because the responder keeps no record of what it has sent (2 above), it cannot
tell whether a record was multicast "recently", which is what the RFC's choice
turns on.

## How it was measured

A scratch program of about a hundred lines, built in Claude's workspace against
this repository's own `SecretPrinter.Responder` and
`tests/SecretPrinter.TestKit` at commit `e42c034`, on Linux with .NET SDK
10.0.112. It builds an advertisement with `AdvertisementBuilder.Build`, makes a
real `MdnsResponder` over the test kit's `FakeTransport`, hands it datagrams
through `HandleAsync`, and prints what the transport was asked to send. The
program is not in this repository, and nothing in it is a test: it shows what
the code does, and a test would have to say what it should do.

The query with a known answer was made by building a response with one question
and one answer and clearing the response bit and the authoritative bit, and it
was parsed back before use to confirm it read as a query with one answer.

## What the same run showed besides

These back statements the README was given the same day
([the finding](2026-10-07-the-readme-was-read-against-the-code.md)).

| Put to the responder | What it sent |
| --- | --- |
| A `PTR` question for `_ipp._tcp.local` from source port 50000, identifier `0xBEEF` | One unicast response to that address and port, identifier `0xBEEF`, the question repeated, one `PTR` answer with TTL 10 |
| A `PTR` question for `_services._dns-sd._udp.local` | One multicast response, a `PTR` at that name |
| An `A` question for `secretprinter.local` | One multicast response, the `A` record, TTL 120 |
| An `SRV` question for the instance name | One multicast response, the `SRV` record, TTL 120 |
| `PTR` questions for `_ipps._tcp.local` and `_printer._tcp.local` | Nothing |
| A probe, with an IPv6 companion interface present | Three queries over IPv4 and three over IPv6 |
| Three announcements, the same | Three over IPv4, none over IPv6 |
| The goodbye, the same | One over IPv4, none over IPv6 |

The advertisement was built from thirteen TXT entries as a printer might publish
them. Published: `txtvers`, `rp`, `pdl`, `Color`, `usb_MFG`, `usb_MDL` and
`product` as given, then `ty` with the proxy's instance name, `note`, and the
proxy's `UUID`. Dropped: `Scan`, the printer's `ty`, `adminurl`, the printer's
`UUID`, the printer's `note` and `Fax`.

## What it costs

More multicast packets on the client network than the standard allows, and
answers a client had already said it held. In every run on record in which the
first two were seen, pages printed. No harm from any of the four has been
observed, and none has been looked for.

## The departures the README already stated

These are decisions, each in the requirement that makes it, and are not part of
this finding:

- **Only link-local `AAAA` records are published,** where §6.2 asks for every
  address valid on the interface (`REQ-ADV-021`).
- **An address change is not followed,** where §8.4 asks a host to announce its
  new addresses (`REQ-ADV-021`).
- **Probes ask for multicast answers,** where §8.1 says they should ask for
  unicast ones (`REQ-ADV-023`).
- **A name conflict withdraws the service,** where §9 has a host choose another
  name (`REQ-ADV-024`).
- **The service makes a printer visible beyond its own link,** which is the
  second item under "Read this before installing".

One more is open and not a decision: whether an IPv4 answer sent by unicast
carries TTL 255, as §11 asks
([the finding of 2026-09-06](2026-09-06-unicast-ttl-gap.md)).

## What this does not show

- **Any of the four on a Windows machine today.** The measurement ran on Linux
  against a fake transport. The code it exercised has no platform-specific
  part, and the two network observations are from 2026-10-02.
- **That these are the only ones.** RFC 6762 was not read through against the
  code. Not looked at, among other things: a query marked truncated, whose
  known answers continue in a following packet (§7.2); the TTL values the
  service publishes against §10; and what the resolver on the printer side
  does as a querier, beyond the schedule `REQ-RES-008` describes.
- **What a client makes of any of it.**
- **What building them would change on a network.** Suppressing answers and
  spacing them out changes what an iPhone receives while it is discovering the
  printer, and each of the four would need a run on hardware.

## Not decided

Whether these are built before release 0.1.0 or stated in it as known problems.
That is a question of what the first release waits for, which is Edwin's.

*(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5:
decided by Edwin West on 2026-10-07. Release 0.1.0 ships with the four stated
as known problems, and they are built afterwards. Claude put the two courses
to him and recommended this one, for these reasons: the four have been in
every build that has printed, and the only cost seen is more multicast packets
on the client network; the README lists them under Known problems, so nothing
is hidden; and building them changes what an iPhone receives while it is
discovering the printer, which needs runs on hardware whose number could not
be said beforehand. What it costs: the first release knowingly misses two
requirements of RFC 6762, and says so.)*
