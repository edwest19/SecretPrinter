# A locally looped IPv6 multicast datagram is attributed to the sending adapter

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-14. Reviewed by a human before merge.*

**Status: measured.** Three probe datagrams, one run, one machine. The socket
half of REQ-ADV-018 and REQ-ADV-020 will be covered by a test rather than by
evidence — the decision that rests on this measurement is recorded at the end of
this document. Nothing in the product changed as a result; this finding exists
so that when the receive path is written, the test design rests on an
observation rather than an expectation.

## Why this was measured before writing any code

The handoff of 2026-09-13 left one question to be answered before the receive
path was written, because the answer changes what the code must expose:

> **How is the socket half tested?**

`MdnsSocketTests` runs against real adapters. The responder half is easy —
`FakeTransport` with a datagram whose `ArrivedOn` is an IPv6 entry. The socket
half needs a real datagram to arrive on a real socket, and the only way to
produce one on demand is to send it from this host.

Three options were on the table. The first — send to `ff02::fb` from a second
socket and assert arrival — is the only one that exercises the actual path. Its
availability rests on a question nobody on this project had asked:

**When Windows loops a multicast datagram back to a socket on the same host,
what interface index does `IPV6_PKTINFO` report for that copy?**

[The Listen6 finding](2026-09-13-ipv6-pktinfo-arrival.md) measured 62 datagrams
arriving **from the link** and every one reported the adapter's index. It says
nothing about a locally looped copy, which never travels on the link and could
reasonably be attributed to loopback instead. Had the answer been index 1, a
test asserting attribution would have failed against correct product code, and
the option would have been unavailable — discovered only after the code was
written, or worse, mistaken for a product bug.

## What was measured

Instrument: `tools/SecretPrinter.Loop6`, written for this question. It opens a
receiving socket with the same options, in the same order, as
`MdnsSocket.OpenIPv6` — the correspondence is listed line by line in that tool's
header so a reader can check it rather than take it on trust — and a second,
separate socket that sends. Both are in one process, because the test whose
feasibility is in question would be in one process.

`IPV6_MULTICAST_LOOP` is **read and never set**, because the product leaves it
at the operating system default per the decision of 2026-09-13. A run that set
it would measure a configuration the service does not use.

Environment:

| | |
| --- | --- |
| Machine | `DESKTOP-URULEFH`, Windows 10.0.19045.0 |
| Adapter | `Ethernet 2`, `192.168.1.234` (client network) |
| IPv4 index | 13 |
| IPv6 index | 13 |
| Group joined | `ff02::fb` on interface index 13 |
| Sending socket | `[::]:59668`, `IPV6_MULTICAST_IF` 13, `IPV6_MULTICAST_HOPS` 0 |
| `IPV6_MULTICAST_LOOP` | `True` — read from the OS default, not set |

Receiving socket options read back from the OS: bound to `[::]:5353`,
`DualMode` false, `ExclusiveAddressUse` false, `SO_REUSEADDR` true,
`IPV6_PKTINFO` true, `IPV6_MULTICAST_HOPS` 255.

The payload is not a DNS message — an ASCII marker, a 16-byte random nonce, and
a sequence byte, 45 bytes in total. It is deliberately unparseable as mDNS so
that no implementation could act on it, and the nonce is what lets the receiver
pick out its own datagram without parsing anything.

## Result

| Probe | Returned | Arrival index | Destination reported | Source |
| --- | --- | --- | --- | --- |
| 0 | yes | 13 | `ff02::fb` | `[fe80::ea9:ba0f:1890:48a9%13]:59668` |
| 1 | yes | 13 | `ff02::fb` | `[fe80::ea9:ba0f:1890:48a9%13]:59668` |
| 2 | yes | 13 | `ff02::fb` | `[fe80::ea9:ba0f:1890:48a9%13]:59668` |

**Three sent, three returned. Every one reported index 13 — the adapter's index,
not the loopback index.**

The committed tool is the instrument that produced this result. It was not
restructured afterwards.

## What this establishes, and exactly how far

**Established:** on this platform, a multicast datagram sent from this host and
looped back locally is attributed by `IPV6_PKTINFO` to the adapter it was sent
through, not to loopback. A test may send to `ff02::fb` from a second socket and
assert that `MdnsSocket.ReceiveAsync` returned an `ArrivedOn` naming that
adapter.

**Established incidentally: hop limit 0 is delivered locally on Windows.** The
run used `IPV6_MULTICAST_HOPS` 0 and all three probes came back. That confirms
an expectation the tool's own header flagged as unverified — that
`IPV6_MULTICAST_LOOP` governs local delivery independently of the hop limit.

**Not established: that nothing went on the wire.** Hop limit 0 is specified to
confine a datagram to the sending node, and that is why the tool defaults to it.
This run shows the copy came back; it does not show that no copy left the
adapter. Proving non-transmission needs a capture on a second host. No claim in
this project depends on it, so none is made.

**Not established, and not establishable here:** whether the reported number is
the IPv6 numbering or the IPv4 numbering. Both families report index 13 for this
adapter. Same limitation, same reason, as
[the Listen6 finding](2026-09-13-ipv6-pktinfo-arrival.md) and
[the parity finding](2026-09-13-interface-index-parity.md). `Loop6` prints both
indexes and states the limitation in its own output.

**Not established: that the product's socket attributes the same way.** `Loop6`
duplicates `MdnsSocket.OpenIPv6`'s configuration; it does not call it. That
duplication is deliberate — it means the thing being measured cannot silently
change when the product changes — but it also means this run makes the test
design *available* rather than making the test unnecessary. The test is what
will show the product does it.

**The sample is three datagrams, not 62.** Smaller than the Listen6 run by an
order of magnitude. The behaviour being measured is a deterministic property of
the operating system's delivery path rather than a statistical one, and three
identical results with no zeros and no other index is taken as sufficient to
choose a test design. It would not be sufficient to support a requirement.

## A dependency the test must declare

The test this enables depends on `IPV6_MULTICAST_LOOP` defaulting to on. This
run read it as `True` and did not set it. The product does not set it either,
so the test and the service share the same dependency — but a default that
changes in a future Windows build would break the test, and a broken test should
name its own cause.

**The test's name and comment must say that it depends on the OS default.** A
failure should then read as "the loopback default changed" rather than as "IPv6
receive is broken", which is the diagnosis it would otherwise invite.

## A trap for whoever writes the test

`Loop6`'s sending socket binds an ephemeral port, so its datagrams arrive with a
source port other than 5353. Through the product, that makes
`MdnsDatagram.IsLegacyUnicastQuerier` true (RFC 6762 §6.7), which changes how a
responder answers.

A test built on this pattern must either bind its sender to 5353 as well — the
port is shareable, which is why `SO_REUSEADDR` is set — or expect the
legacy-unicast path deliberately. Not a defect, but it would produce a puzzling
failure.

This is also the first opportunity to exercise the IPv6 legacy-unicast path,
which [the Listen6 finding](2026-09-13-ipv6-pktinfo-arrival.md) recorded as
unmeasured: all 62 of its datagrams came from source port 5353. Whether to take
that opportunity here or leave it as a separate step is not decided by this
finding.

## Incidental observation

**No ambient traffic was discarded.** The tool reports zero other datagrams. The
receiver is open only for the milliseconds between each send and its match, so
the link's ordinary IPv6 mDNS traffic had no window in which to arrive. This is
expected and is not a sign the receiver was inactive — it read all three of its
own probes. `Listen6` is the tool for characterising ambient traffic; this one
deliberately discards it unprinted so a transcript contains only this machine's
own addresses.

## Decision recorded: the socket half is covered by a loopback test

**The socket half of REQ-ADV-018 and REQ-ADV-020 will be covered by a test that
sends to `ff02::fb` from a second socket in the same process and asserts that
`MdnsSocket.ReceiveAsync` returned an `ArrivedOn` naming the adapter.** Decided
by Edwin West, 2026-09-14, on the strength of the measurement above.

The reasoning: of the three options left open by the handoff of 2026-09-13, it
is the only one that exercises the path the requirement describes. This project
holds that code coverage beats evidence, and a marker with no test is what the
results-file mechanism exists to prevent.

The two rejected options, recorded so the choice is not re-made by default:

**A unicast send to `[::1]:5353`** would prove that a datagram arriving on the
IPv6 socket is attributed to *something*, but loopback is not the joined
interface, so the arrival index would be 1 rather than the adapter's. It tests
the plumbing while avoiding the question.

**Evidence coverage, citing this finding and a `Loop6` run**, would leave
REQ-ADV-018 and REQ-ADV-020 marked without a test, in the area of the code where
the project has the least prior verification — `MdnsSocket.ReceiveAsync` is
currently called by no test at all, on either family.

That last point is worth stating plainly rather than leaving implicit: **this
decision also gives the IPv4 receive path its first test.** REQ-ADV-015 has
carried an implementation marker on `ReceiveAsync` since before IPv6 work began,
and every test exercising receive goes through `FakeTransport`. Whether to add
the IPv4 counterpart in the same commit or a later one is not settled here.

Two conditions attach to the decision, both from the sections above: the test
must name its dependency on the `IPV6_MULTICAST_LOOP` default in its own name and
comment, and it must deal deliberately with the source-port question rather than
inherit it.

## What happens next

1. The receive path and the `MdnsResponder` family-keying fix, as one commit,
   because neither is safe alone. That earns REQ-ADV-018 and REQ-ADV-020
   together and takes binding gaps from 7 to 5.
2. `tools/SecretPrinter.Loop6` has no scheduled deletion date and no
   `[Requirement]` markers. Whether it survives the arrival of the test it
   enabled is an open question, recorded in its `.csproj` and not settled here.
