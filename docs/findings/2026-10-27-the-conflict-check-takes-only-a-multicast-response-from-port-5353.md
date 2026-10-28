# The conflict check takes only a multicast response from port 5353

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-27. Reviewed by a human before merge.*

**Status: built and measured against the code by tests; not run on a network,
and no release carries it. On the client network, the responder now reads a
response for records about the names it claims only when the response was sent
to the mDNS group, 224.0.0.251 or ff02::fb, and came from UDP port 5353, as
RFC 6762 §6 requires (README `REQ-ADV-030`). Any other response, and one whose
destination is not known, is silently ignored. Until now every response that
arrived on a client interface was read for conflicts, whatever its destination
and source port; releases `0.1.0` to `0.5.0` do so. Nothing the service sends
changes.**

*(Status note, later on 2026-10-27, by Claude, Claude Opus 5.5: committed and
pushed as `0b51fcc`; CI run 172 passed. Run once on a network that evening, on
FIOS-STB-01, in a build of `0b51fcc` made on the development machine and not
signed: its probe at startup came back clear, and a page printed from an
iPhone at the first attempt. No release carries it. See [the finding of the
run](2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name. The addresses in the tests are made up.

## Why this was done

[The finding of 2026-10-26 on the lookups](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md)
raised it, under "Raised, not decided": "The responder's conflict check takes a
response from any port", adding that whether §6's rule on unicast responses
applies to it as well was not worked out. The handoff of 2026-10-26 listed it
second among the items for Claude to settle. It was not put to Edwin West as a
choice; he reviews it before the commit.

The item listed first, that the startup wait does not use what is heard, was
settled the same day as not changed; the reasons are in a status note on
[the finding of 2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md).

## What RFC 6762 says

Read on 2026-10-27 through a fetch tool, from
`https://www.rfc-editor.org/rfc/rfc6762.txt`. The tool quotes in pieces of at
most about 125 characters, and trims some sentences to fit; the two sentences
of §6 below were quoted in full, and match the quotation in the finding of
2026-10-26, which was read from the same RFC.

§6, Responding:

> Multicast DNS implementations MUST silently ignore any Multicast DNS
> responses they receive where the source UDP port is not 5353.

> A Multicast DNS querier MUST only accept unicast responses if they answer a
> recently sent query (e.g., sent within the last two seconds) that explicitly
> requested unicast responses. A Multicast DNS querier MUST silently ignore all
> other unicast responses.

§8.1, Probing, in a piece the tool trimmed: "the probes SHOULD be sent as "QU"
questions with the unicast-response bit set," "to allow a defending host to
respond immediately via unicast".

§9, Conflict Resolution, speaks of "a Multicast DNS response message containing
a record with the same name, rrtype and rrclass, but inconsistent rdata". The
tool reported that §9 says nothing of source ports or unicast.

## Whether the two rules apply to the conflict check

- **The port rule** applies to "Multicast DNS implementations", which the
  responder is. It applies to every response, whatever the responder does with
  it.
- **The unicast rule** is written for a querier. The responder is one while it
  probes: a probe is a query (§8.1). Its probes ask for multicast answers, a
  departure from §8.1's SHOULD that `MdnsResponder.Probing.cs` and README
  `REQ-ADV-023` record: the service shares UDP port 5353 with the Windows DNS
  Client (`REQ-ADV-014`), and which of two sockets sharing a port receives a
  unicast datagram has not been established, so a unicast answer could be
  missed. The responder sends no other query. So no unicast response answers a
  query of its own that asked for one, and §6 has a querier silently ignore
  every such response. After probing, the check reads what it hears for
  conflicts (§9); §9 does not say a unicast response counts there, and §6 says
  to ignore it. A unicast to this machine's port 5353 may also have been meant
  for the Windows DNS Client's socket: an answer to a question that client
  asked with the unicast-response bit set.

Both rules are taken as written. Neither is a judgement of Claude's.

## What the conflict check did, and what it does now

`MdnsResponder.HandleAsync` passes every response that arrives on a client
interface to `NoteConflicts` (`MdnsResponder.Probing.cs`), which looks for a
record about a name the responder claims that it would not itself publish
(`REQ-ADV-024`). The first one found withdraws the service until it is
restarted.

- **Before:** `NoteConflicts` read every response it was given, whatever its
  source port, and whether it had been sent to the group or by unicast to this
  machine.
- **Now:** it returns at once, reading nothing, unless
  `MdnsDatagram.SentToGroup` is true and the source port is 5353.
  `SentToGroup` is true when the destination address that IP_PKTINFO or
  IPV6_PKTINFO reports is `224.0.0.251` or `ff02::fb`; it was built for
  `REQ-RES-011` on 2026-10-22, and the lookups use it since `REQ-RES-012`. A
  datagram whose destination is not known is not taken as one sent to the
  group. Nothing is logged, counted or kept for a response ignored this way.

The comment in `HandleAsync` where a response is passed on now says so too.

## Security

Nothing new is sent, nothing new is opened, and nothing is parsed that was not
parsed before. The change only narrows which responses can withdraw the
service.

- **A device on the client network** can still make the service withdraw, by
  answering for its names by multicast from port 5353. README `REQ-ADV-024`
  accepts that for a home network, since mDNS has no authentication. What it
  can no longer do is the same by unicast to this machine, or from another
  port. So this is conformance with the standard, not a new defence, and it is
  not described as one.
- **What changes in practice** is that a response §6 says to ignore no longer
  withdraws the service: one meant for the Windows DNS Client, or one from a
  device that does not follow §6. Whether either has ever arrived is not known.

## Why this should cost nothing on this network

No conflict has been logged on FIOS-STB-01 in the reads of its log recorded on
2026-10-25 and 2026-10-27, where the service probed after each loss and every
probe came back clear. A change that only stops some responses being read
cannot turn a clear probe into a conflict. A device that defends a name by
unicast, though §6 and §8.1 say a defender answers a probe that does not ask
for unicast by multicast, would no longer be heard; none is known on the
client network.

## The tests

Seven new tests in a new file, `tests/SecretPrinter.Responder.Tests/ConflictSourceTests.cs`,
marked `REQ-ADV-030` and registered in that suite's `Program.cs`. Each hands
the responder the same response, another device's address for the host name
the responder claims, which is a conflict when it is read; only its source
port and destination change. They were written first and run against the
code without the change: the five that a response is ignored failed, and the
two controls passed, as predicted. The 205 tests already in the suite passed.

| Test | Without the change |
| --- | --- |
| A response sent to 224.0.0.251 from port 5353, with another address for the host, is a conflict | passed (a control) |
| A response sent to ff02::fb from port 5353, with another address for the host, is a conflict | passed (a control) |
| The same response from a port other than 5353 is silently ignored, and a later one from 5353 is still read | failed |
| The same response sent by unicast to this machine is silently ignored | failed |
| The same response sent by unicast over IPv6 is silently ignored | failed |
| The same response whose destination is not known is silently ignored | failed |
| While probing, a response from a port other than 5353 leaves the probe clear | failed |

The port test tries ports 53, 5354, 49152 and 65535, each with a new
responder, and after each hands it the same response from port 5353 to show
that nothing of the ignored one was kept. An ignored response must leave no
conflict, report nothing to the service, and send nothing.

### The tests that changed

Many tests built the responses they hand the responder without a destination,
which a test can do and a socket cannot. Under the change such a response is
no longer read, and a test that a response is *not* a conflict would then pass
for the wrong reason. To find every one, the code without the change was given
a temporary line that wrote a stack trace for each response without a
destination that reached the conflict check, and the responder suite was run:
43 such responses, in tests in seven files. Each place that builds them now
gives the response the group as its destination, through a new helper,
`MdnsResponderTests.GroupFor`, which gives `224.0.0.251` for an IPv4 interface
and `ff02::fb` for an IPv6 one. Run again with the temporary line, none was
left; the line was then removed, and the source file compared with the
committed one. The files: `MdnsResponderTests.cs`, `MdnsProbeTests.cs`,
`LinkLocalAnsweringTests.cs`, `NsecAnsweringTests.cs`, `NsecConflictTests.cs`,
`OncePerSecondTests.cs` and `RandomAnswerDelayTests.cs`. No test changed what
it checks.

In the service suite, `OfferingTests.HearConflict` hands the responder a
conflict over IPv6, and now gives it `ff02::fb`. With the change and without
that destination, four of its tests failed; with it, all pass. No other test in
the service suite hands a response to the responder.

## Breaking each claim on purpose

Four mutations, each made alone in `NoteConflicts`, built, run against the
responder suite, and undone; the file was compared with a saved copy after
each. Each was predicted before it was run, and each failed exactly the tests
predicted.

| Mutation | Caught by |
| --- | --- |
| The port test removed | the two port tests |
| The group test removed | the IPv4 unicast, IPv6 unicast and unknown-destination tests |
| Only `224.0.0.251` taken as the group | the IPv6 control, and `MdnsProbeTests.Different_address_for_our_host_is_a_conflict` and `First_conflict_is_reported_once`, which hear a conflict over IPv6 |
| Every response ignored | 17 tests: both controls, the port test at its "still read" step, and every existing test that hears a conflict |

The last was first written as `if (true)`, which does not build here: the
compiler warns that the code after it cannot be reached, and warnings are
errors in this repository. It was written again as a condition that is always
true but not known to be at compile time.

## What a run on a network would need to show

That the service, built with this change, probes clear at startup and after a
loss, offers the printer, and that a page prints from the iPhone. Nothing here
predicts a difference on this network; a run would show that nothing broke.

## What this does not show

- **Any of it on a network, on Windows, or in a release build.** The tests run
  against a fake transport.
- **A response from another device received over IPv6 on Windows.** A
  conflict heard over IPv6 now depends on its destination being read as
  `ff02::fb`. Over IPv4 that was shown on a real socket by `MdnsSocketTests` on
  2026-10-22, and by the run of 2026-10-26, where the lookups, which take only
  what `SentToGroup` calls sent to the group, were answered. Over IPv6 no test
  had read it back, so one check was added to
  `MdnsSocketTests.IPv6_arrival_is_attributed_to_the_ipv6_entry`, which sends
  a datagram from a second socket on the same machine to `ff02::fb` and reads
  it back: that it is read back as sent to the group. That test needs IPv6,
  which Claude's workspace does not have, so it was skipped there. On the
  development machine, on 2026-10-27, before the commit, it ran and passed,
  read by Claude from the results file the run wrote there. What it shows is
  this machine's own datagram, looped back; one from another device was not
  looked at.

## Raised, not decided

- **The probe** (`tools/SecretPrinter.Probe`), carried from the finding of
  2026-10-26: it asks as a legacy querier under §6.7, from a port the
  operating system chooses, and reads the unicast replies that brings. Whether
  §6's port rule should apply to it was not worked out. *(Status note,
  2026-10-28, by Claude, Claude Opus 5.5: worked out and changed; see the
  status note of that day on the same item in
  [the finding of 2026-10-26](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md),
  and [the finding of the change](2026-10-28-the-probe-keeps-only-a-reply-from-port-5353-to-its-own-question.md).)*
- **Goodbyes** are heard and not acted on, by Edwin's choice of 2026-10-22,
  carried.

## How it was measured

- **In Claude's workspace, on Linux,** with .NET SDK 10.0.113 and runtime
  10.0.13, Ubuntu's build from its own package archive, because Microsoft's
  download servers cannot be reached from the workspace. NuGet cannot be
  reached either, so the service's one package,
  `System.ServiceProcess.ServiceController`, was taken from the SDK's own
  folder, in a scratch copy of the repository outside it, as on 2026-10-26.
  That is not the build a release makes.
- **Before the change**, from the clean commit `dcababb`: the responder suite,
  205 tests, all passing; the service suite, 125 passing and 1 skipped (an
  IPv6 test this workspace cannot run), with `OfferingTests` already giving
  its response a destination, which changes nothing without the change.
- **After the change:** 28, 27, 40 (9 skipped, IPv6), 62, 86, 212 and 125 (1
  skipped, IPv6), every other test passing. SpecCheck: 111 requirements, 23
  assemblies, 18 evidence rows, 597 test records, 93 requirements with a
  passing test, 92 covered by code and 17 by evidence, 108 binding of which
  106 covered, and 2 binding gaps, `REQ-ADV-019` and `REQ-ADV-020`, whose
  tests need IPv6. The new row, `REQ-ADV-030`, is `OK`.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Responder/MdnsResponder.Probing.cs`: the check at the top
  of `NoteConflicts`, its `REQ-ADV-030` marker and remarks, and a note in the
  header.
- `src/SecretPrinter.Responder/MdnsResponder.cs`: the comment in `HandleAsync`
  where a response is passed to `NoteConflicts`, and a note in the header.
  No code changed in this file.
- `tests/SecretPrinter.Responder.Tests/ConflictSourceTests.cs`: new, seven
  tests.
- `tests/SecretPrinter.Responder.Tests/Program.cs`: the new tests registered.
- `tests/SecretPrinter.Responder.Tests/`: `MdnsResponderTests.cs` (the new
  helper `GroupFor`), `MdnsProbeTests.cs`, `LinkLocalAnsweringTests.cs`,
  `NsecAnsweringTests.cs`, `NsecConflictTests.cs`, `OncePerSecondTests.cs` and
  `RandomAnswerDelayTests.cs`: responses given the group as their destination,
  and a note in each header.
- `tests/SecretPrinter.Service.Tests/OfferingTests.cs`: the same, for
  `HearConflict`.
- `tests/SecretPrinter.Mdns.Tests/MdnsSocketTests.cs`: one check added to the
  IPv6 arrival test, and a note in the header.
- `README.md`: a new row, `REQ-ADV-030`; a Known problems entry, and the
  section's note.
- Status notes on the findings of
  [2026-10-26 on the lookups](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md)
  and of [2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md),
  the second recording that the startup wait was settled as not changed.
- This finding.
