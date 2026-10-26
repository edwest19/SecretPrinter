# A lookup takes only a multicast response from port 5353

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-26. Reviewed by a human before merge.*

**Status: built and measured against the code by tests; not run on a network,
and no release carries it. A lookup on the printer-side interface, the path
every question the service puts to the printer takes, now takes its answer
only from a response that was sent to the mDNS group and came from UDP port
5353, as RFC 6762 §6 requires of a querier (README `REQ-RES-012`). Until now
it took a response that arrived on the printer-side interface whatever its
destination and source port; releases `0.1.0` to `0.5.0` do so. The printer's
records heard between lookups were already held to both rules (`REQ-RES-011`).
Nothing the service sends changes.**

*(Status note, later on 2026-10-26, by Claude, Claude Opus 5.5: committed and
pushed as `1b4358b`; CI run 168 passed. Edwin West chose that it wait for a
network run together with the next change. That change answers the first of
the items carried from the finding of 2026-10-22 under "Raised, not decided"
below, no TTL capped: no answer is now believed for
longer than 120 seconds (README `REQ-RES-013`). See [its
finding](2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name. The addresses in the tests are made up; the printer's
host name in them is redacted to `EPSON000000`, as in
[the audit of 2026-09-04](2026-09-04-pre-publication-audit.md).

## Why this was done

[The finding of 2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md)
raised it, under "Raised, not decided", as Claude's to settle as its own
change: "a lookup takes a unicast response, though none of its questions ask
for one, and a response from any source port". The handoff of 2026-10-25 put
it first on Claude's list. It was not put to Edwin West as a choice; he
reviews it before the commit.

## What RFC 6762 says

Read on 2026-10-26 through a fetch tool, from
`https://www.rfc-editor.org/rfc/rfc6762.txt`. The tool quotes at most about 125
characters at a time; the sentences below were given in pieces and are joined
here with the RFC's line breaks as spaces. They match the quotation in the
finding of 2026-10-22, which was read from the same RFC.

§6, Responding:

> A Multicast DNS querier MUST only accept unicast responses if they answer a
> recently sent query (e.g., sent within the last two seconds) that explicitly
> requested unicast responses. A Multicast DNS querier MUST silently ignore all
> other unicast responses.

> The source UDP port in all Multicast DNS responses MUST be 5353 (the
> well-known port assigned to mDNS). Multicast DNS implementations MUST
> silently ignore any Multicast DNS responses they receive where the source UDP
> port is not 5353.

And of the responder, in the same section: a response's destination address
"MUST be the mDNS IPv4 link-local multicast address 224.0.0.251 or its IPv6
equivalent FF02::FB, except when generating a reply to a query that explicitly
requested a unicast response". The RFC then lists three cases where unicast is
allowed (a question with the unicast-response bit set, a legacy query from a
port other than 5353 under §6.7, and a query sent by unicast), and says:
"Except for these three specific cases, responses MUST NOT be sent via
unicast". The three cases are given here in Claude's words; the tool gave them
in paraphrase.

## What the lookups did, and what they do now

Every question the service puts to the printer goes through
`PrinterResolver.ResolveAsync`: the startup wait, the watch's reconfirmations,
the questions to a printer held unreachable, and each print job's lookup
(`ServiceHost.cs`, read on 2026-10-26). It sends its questions by multicast, to
`224.0.0.251:5353`, from the resolver's socket, which `MdnsSocket.Open` binds to
UDP port 5353, and builds both of them, the SRV-and-TXT question and the
follow-up question for the host's address, with the unicast-response bit clear.
None is any of the three cases above.

- **Before:** `ReceiveAnswerAsync` took any response that arrived on the
  printer-side interface (`REQ-RES-006`), from any source port, whether it had
  been sent to the group or by unicast to this machine.
- **Now:** after the interface test, a datagram is skipped unless
  `MdnsDatagram.SentToGroup` is true, which it is when the destination address
  that IP_PKTINFO reports is `224.0.0.251` or `ff02::fb` (built for
  `REQ-RES-011` on 2026-10-22), and its source port is 5353. A datagram whose
  destination is not known is not taken as one sent to the group, as
  `PrinterAnnouncements` already treats it.

## Why this should cost nothing on this network

If the printer answered the service's questions by unicast, every lookup would
now go unanswered, and the printer would never be offered. The record says it
does not:

- **On 2026-10-21** a listener on the development machine, on the printer-side
  network, heard the printer's reply to FIOS-STB-01's SRV-and-TXT question
  eight times, each within 0.3 seconds of the question
  ([finding](2026-10-21-the-printer-announces-itself-when-it-starts.md), "What
  the listener heard"). A unicast reply to FIOS-STB-01 would not have reached
  the listener on another machine. The listener did not print a destination
  address, so what this shows is that those replies were not sent by unicast
  to FIOS-STB-01.
- **RFC 6762 §6** requires a responder to answer these questions by multicast,
  as quoted above.

Not shown: whether the printer's answer to the follow-up question for its
host's address is ever sent by unicast. A lookup asks that question only when
an SRV arrives without the address alongside it. The listener's table, which
included the questions naming the printer, shows it once, at 22:01:40.7,
during the printer's announcements, and no reply attributed to it; it shows no
such question after any of the eight replies of 693 bytes.

## Security

Nothing new is sent, nothing new is opened, and nothing is parsed that was not
parsed before. The change only narrows what a lookup believes.

- **A device on the printer-side network** could already answer a lookup with
  records of its own, and still can, by multicast from port 5353. What it can
  no longer do is answer by unicast, or from another port. The printer-side
  network is the one the service trusts for the printer's address in any case
  (`REQ-RES-006`), and a forged address still meets the pinned certificate
  (`REQ-SEC-013`). So this is conformance with the standard, not a new
  defence, and it is not described as one.
- **What changes in practice** is only which responses a lookup believes: one
  sent by unicast to this machine, or from a port other than 5353, is no
  longer taken as the printer's address. Whether either has ever arrived on
  the printer-side interface is not known.

## The tests

Five new tests in `PrinterResolverTests.cs`, marked `REQ-RES-012`. They were
written first and run against the unchanged resolver: all five failed, and the
74 tests already in the suite passed.

| Test | Against the unchanged code |
| --- | --- |
| A lookup does not take a response sent by unicast, since none of its questions asks for one | failed |
| A lookup does not take a response from a port other than 5353 | failed |
| A lookup does not take a response whose destination is not known | failed |
| A lookup answered only by unicast fails as unanswered | failed |
| A lookup answered only from a port other than 5353 fails as unanswered | failed |

In the first three, the lookup's question is answered first by a response that
must be ignored, naming an address the printer does not hold, and then by the
printer's own multicast answer; a lookup that took the first would report the
wrong address. The last two are answered only by the response that must be
ignored, and must fail as unanswered.

Two test helpers changed, because they built the printer's answer without a
destination, and a lookup now ignores that:

- `PrinterResolverTests.PrinterReply` now marks its reply as sent to
  `224.0.0.251` unless told otherwise (its `toGroup` parameter defaults to
  true; it defaulted to false). Its source port was already 5353.
- `ServiceHostTests.ServiceReply` now gives its reply the destination
  `224.0.0.251`.

No other test builds an answer that reaches a lookup: `PrinterSideTests`
already sent its announcement to the group, and the other datagrams the tests
build go to the responder, not to a resolver.

## Breaking each claim on purpose

Two mutations, each made alone in `PrinterResolver.cs`, built, run against the
resolution suite, and undone; the file was compared with a saved copy after
each.

| Mutation | Caught by |
| --- | --- |
| The port test removed: a response sent to the group from any port taken | the two port tests |
| The group test removed: a response from port 5353 taken however it was sent | the unicast test, the unknown-destination test, and the answered-only-by-unicast test |

## What a run on a network would need to show

That the service, built with this change, still finds the printer at startup
and offers it, that a page prints from the iPhone, and that over the hours
after, it does not lose the printer more often than the build before it. A
lookup that went unanswered because the printer's answer came by unicast would
look, in the log, like any other unanswered question.

## What this does not show

- **Any of it on a network, on Windows, or in a release build.** The tests
  run against a fake transport.
- **That the printer never answers a lookup by unicast.** See "Why this should
  cost nothing on this network".

## Raised, not decided

- **The responder's conflict check takes a response from any port.** On the
  client side, `MdnsResponder` reads every response it hears for records
  about the names it claims (`REQ-ADV-024`), and does not look at the
  response's source port. §6's second rule applies to "Multicast DNS
  implementations", not only to queriers. Found while reading the code for
  this change, on 2026-10-26; not changed here, one thing at a time. Whether
  the first rule, on unicast responses, applies to it as well was not worked
  out.
- **The probe** (`tools/SecretPrinter.Probe`) asks from a port the operating
  system chooses, as a legacy querier under §6.7 by design, and reads the
  unicast replies that brings. It is not changed. Whether §6's port rule
  should apply to a legacy querier was not worked out.
- Carried from the finding of 2026-10-22, unchanged: no TTL is capped; the
  startup wait does not use what is heard; goodbyes are heard and not acted
  on, by Edwin's choice.

## How it was measured

- **In Claude's workspace, on Linux,** with .NET SDK 10.0.113 and runtime
  10.0.13, Ubuntu's build from its own package archive, because Microsoft's
  download servers cannot be reached from the workspace. NuGet cannot be
  reached either, so the service's one package,
  `System.ServiceProcess.ServiceController`, was taken from the SDK's own
  folder, in scratch copies of the repository outside it, as on 2026-10-22.
  That is not the build a release makes.
- **Before the change**, from the clean commit `e831fbb`: 28, 27, 49 (9
  skipped, all of them IPv6 tests that this workspace cannot run), 62, 74,
  205 and 126 (1 skipped) tests, every other test passing; SpecCheck: 108
  requirements, 578 test records, 90 requirements with a passing test, 89
  covered by code, 17 by evidence, 105 binding of which 103 covered, and 2
  binding gaps, `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6.
- **After the change**, the same, except: the resolution suite 79; SpecCheck
  109 requirements, 583 test records, 91 with a passing test, 90 covered by
  code, 106 binding of which 104 covered, and the same 2 binding gaps. The
  coverage matrix gains one row, `REQ-RES-012`, `OK`, and no other row
  changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.
- **One prediction was wrong:** Claude said the service suite was the one of
  205 tests. It is the responder suite; the service suite has 126.

## What changed

- `src/SecretPrinter.Resolution/PrinterResolver.cs`: the check in
  `ReceiveAnswerAsync`, its `REQ-RES-012` marker, and a section in the header.
- `tests/SecretPrinter.Resolution.Tests/PrinterResolverTests.cs`: five tests,
  `PrinterReply`'s default, and the header.
- `tests/SecretPrinter.Service.Tests/ServiceHostTests.cs`: `ServiceReply`'s
  destination, and the header.
- `README.md`: a new row, `REQ-RES-012`; a Known problems entry, and the
  section's note.
- Status notes on the findings of
  [2026-10-21](2026-10-21-the-printer-announces-itself-when-it-starts.md) and
  [2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md).
- This finding.
