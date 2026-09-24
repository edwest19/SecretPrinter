# "iOS asks only over IPv6" was in more places than the list said

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-24. Reviewed by a human before merge.*

*Status and the entries for source, tests and the experiment tool updated the
same day, when those places were corrected, by Claude (Anthropic model, Claude
Opus 5.5) at the direction of Edwin West. The status first said they were not
yet corrected. Reviewed by a human before merge.*

**Status: all nine places are corrected. The documents were corrected in
`33b2e6d`, with this finding. The places in source, tests and an experiment
tool were corrected in the commit that updated this status, after a build, the
tests and SpecCheck. What the service does is unchanged: that commit changed
comments, one assertion message and the text of one refusal message.**

## The claim, and why it is wrong

The claim is that iOS sends its mDNS questions over IPv6 only. It came from
captures taken at FIOS-STB-01
([2026-09-06](2026-09-06-ipv6-mdns-transport.md),
[2026-09-14](2026-09-14-ipv6-is-the-only-transport.md)). Those record what
reached that machine, not what the phone sent.

On 2026-09-23 another machine on the client network saw IPv4 mDNS queries from
the iPhone, `192.168.1.152`
([2026-09-23](2026-09-23-the-goodbye-and-ipv4-on-the-wire.md)). On 2026-09-24
the iPhone was seen sending the same queries over IPv4 and over IPv6. The IPv6
copies reached FIOS-STB-01. The IPv4 copies reached the dev box, which is behind
the same upstairs access point as the phone, and did not reach FIOS-STB-01
([2026-09-24](2026-09-24-ipv4-mdns-from-behind-the-access-point.md)).

What stands: on this network, a responder at FIOS-STB-01 holding an IPv4 socket
alone did not receive the iPhone's questions, and IPv6 is what made discovery
work. `REQ-ADV-018` is unchanged. In the 2026-09-24 window the cause was the
path the IPv4 copies took, not a preference in iOS, because the phone sent
both. Where on that path they are lost is not established. Whether the phone
sent IPv4 on 2026-09-06 was not measured.

## The mistake

The 2026-09-23 finding corrected two findings and listed four places that still
carried the claim, under "The same claim, not corrected here". The 2026-09-24
access-point finding then said the places that still carry the claim "are
listed in that day's finding". Neither says how the list was made. It was not
complete, and the second finding presented it as if it were.

A search of the whole repository, made on 2026-09-24 before any correction,
found five more places. Reading the 2026-09-06 finding in full then found a
second passage in it that the search's phrasings had missed. That passage is
counted below under that finding, which the list already named.

## Where the claim was

Nine places. Each entry gives the original words and whether it is corrected.
Where a document carries its own dated note, the original words are kept there
as well.

**Corrected with this finding:**

1. `docs/findings/2026-09-06-ipv6-mdns-transport.md`, on the 2026-09-23 list.
   Two passages: "A client network offering IPv6 makes iOS query over IPv6
   exclusively, and an IPv4-only responder is invisible there", and "Here the
   client had both and chose IPv6 for mDNS, IPv4 for IPP." Both now say what
   reached FIOS-STB-01. The capture results stand. Dated note at the top.
2. `docs/findings/2026-09-21-a-second-printer-entry-on-the-iphone.md`, on the
   list. "The iPhone queries exclusively over IPv6". It now says the phone was seen
   sending mDNS over both, so an advertisement present only over IPv6 is not
   ruled out.
   Dated note at the top.
3. `docs/operating.md`, under "Uninstalling", on the list. "The goodbyes go out
   over IPv4 only, and iPhones ask over IPv6." The first half is true: the code
   sends goodbyes by the IPv4 entry only (`MdnsResponder.SendGoodbyeAsync`). The
   second half now says that the phone asks over both, that only its IPv6
   questions are answered where its IPv4 does not arrive, and that whether iOS
   applies an IPv4 goodbye to what it learned that way is not established. Note
   in the header.
4. `README.md`, `REQ-ADV-018`, not on the list. The requirement is unchanged.
   Its "Measured:" sentence read: "an iPhone on the client network queried
   exclusively over IPv6 and never over IPv4, so a responder holding an IPv4
   socket alone never receives the question." It now says the iPhone sent the
   same queries over both, only the IPv6 copies reached the service's machine,
   and where the IPv4 copies were lost is not established. The README has no
   place for a dated note inside a requirement table, so the original words are
   kept here.

**Corrected in a second commit (source, tests and a tool, which needed a
build).** Each file's header records the change and points here for the
original words:

5. `src/SecretPrinter.Service/ServiceHost.cs`, a comment on the socket plan, on
   the list: "iOS was measured querying over IPv6 alone, so the client side
   needs both (docs/findings/2026-09-06-ipv6-mdns-transport.md)." It now says an
   iPhone was measured sending the same queries over both, only the IPv6 copies
   reached the service's machine on the network this was built on, and so the
   client side needs both.
6. `src/SecretPrinter.Mdns/MdnsInterface.cs`, not on the list. This is the
   message an operator sees when a client interface has no IPv6: "... has no
   IPv6 configuration, so it cannot receive the mDNS queries iOS sends. iOS was
   measured querying over IPv6 only, so a client interface without it is never
   discovered." It now says the interface cannot receive mDNS queries sent over
   IPv6, that an iPhone was measured sending its queries over both, and that
   where the IPv4 copies do not reach the machine only the IPv6 copies can be
   answered. The refusal itself is unchanged.
7. `src/SecretPrinter.Mdns/MdnsBinding.cs`, a comment on `JoinIPv6`, not on the
   list: "a client interface without IPv6 can never be discovered by iOS, which
   was measured querying over IPv6 only
   (docs/findings/2026-09-06-ipv6-mdns-transport.md)". It now gives the same
   measurement as entry 5, and says that there, starting without IPv6 would
   answer none of the iPhone's queries.
8. `tests/SecretPrinter.Mdns.Tests/MdnsSocketTests.cs`, an assertion message in
   `Interface_without_ipv6_is_rejected`, not on the list: "a client interface
   without IPv6 can never be discovered by iOS". It now reads "a client
   interface without IPv6 cannot receive mDNS queries sent over IPv6". What the
   test checks is unchanged.
9. `tools/SecretPrinter.Respond6/Program.cs`, the header comment, not on the
   list. It said the 2026-09-06 capture "showed an iPhone issuing mDNS queries
   exclusively over IPv6 (to ff02::fb)". It now says the capture held queries
   from an iPhone that had arrived over IPv6 only. The tool is to be deleted
   once IPv6 is in the product, which is a separate item.

The sentence in the 2026-09-24 access-point finding that called the list
complete is corrected with this finding, and the 2026-09-23 finding gains a
dated note pointing here. Its list is left as written.

## Read and left alone

These mention IPv6 and iOS, or IPv6 alone, but do not say what iOS sends:

- `src/SecretPrinter.Mdns/MdnsInterface.cs`, the remarks on `Address`: an
  iPhone that asks over IPv6 is answered with the IPv4 address.
- `src/SecretPrinter.Mdns/MdnsSocket.cs`, the `REQ-ADV-018` marker text: a query
  arriving only over IPv6 is seen.
- `src/SecretPrinter.Responder/MdnsResponder.cs`, the remarks on
  `DescribeByTransport`.
- `tools/SecretPrinter.Listen6/README.md`, on generating IPv6 traffic.
- `tools/SecretPrinter.Respond6/README.md`, which reports what the 2026-09-06
  capture held, not what the phone sent.

A different claim, about names rather than transports, is not part of this
correction: `REQ-ADV-002` says "iOS queries this subtype exclusively", and a
comment in `AdvertisementBuilder.cs` and an assertion message in
`MdnsResponderTests.cs` say the same. The 2026-09-23 finding recorded iOS also
asking for `_universal._sub._ipps._tcp.local`. Not examined here.

## How the search was made

`grep`, case-insensitive, over a fresh clone of `a115809`, outside `.git`:

- First, every file, for the phrasings the known places use: "exclusively",
  "IPv6 alone", "IPv6 only", "only over IPv6", "ask over IPv6", "querying over
  IPv6", "never over IPv4".
- Then, every file except the 2026-09-14, 2026-09-23 and 2026-09-24 findings,
  which record the corrections, for wider ones: "chose IPv6", "prefer IPv6",
  "zero IPv4", "no IPv4", "instead of IPv4", "not over IPv4".
- Then, outside `docs/findings/`, every line of a `.md`, `.cs`, `.json`,
  `.props` or `.yml` file that mentions iOS or iPhone together with IPv4, IPv6,
  `ff02` or "transport".

Every hit was read in context. The later searches found nothing beyond the
passage already found by reading. A search finds only the phrasings it
anticipates; anyone checking this list should search again rather than trust
it.
