# IPV6_PKTINFO reports a usable arrival interface index

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-13. Reviewed by a human before merge.*

**Status: measured.** 62 datagrams across three runs on one machine. The
receive path for REQ-ADV-018 and REQ-ADV-020 can be built on this. Nothing in
the product changed as a result; this finding exists so that when it is built,
the design rests on an observation rather than an expectation.

## Why this was measured before writing any code

REQ-ADV-020 requires the arrival interface of an IPv6 query to be determined
from `IPV6_PKTINFO` rather than inferred. `MdnsSocket.OpenIPv6` has set that
option since 2026-09-06, and `ReadBackIPv6Configuration` confirms Windows
accepted it — which proves only that Windows accepted it. The header of
`MdnsSocket.cs` said so plainly at the time:

> The option is set; that it reports a usable interface index on Windows has not
> been observed by anyone on this project and must not be assumed.

The receive path is a substantial change: a two-socket receive with pending
tasks held across calls, plus a keying fix in `MdnsResponder`. If Windows had
reported index 0, all of that would have been built on a foundation that does
not hold, and the attribution design would have needed rewriting afterwards.
Ten minutes of measurement removed that risk.

## What was measured

The question, stated so it could fail: **when a datagram arrives on a socket
bound to `[::]:5353` with `IPV6_PKTINFO` enabled, does
`SocketReceiveMessageFromResult.PacketInformation.Interface` report a non-zero
index naming the adapter it arrived on?**

Instrument: `tools/SecretPrinter.Listen6`, which opens the socket with the same
options, in the same order, as `MdnsSocket.OpenIPv6`. The correspondence is
listed line by line in that tool's header so a reader can check it rather than
take it on trust. The tool never transmits.

Environment:

| | |
| --- | --- |
| Machine | `DESKTOP-URULEFH`, Windows 10.0.19045.0 |
| Adapter | `Ethernet 2`, `192.168.1.234` (client network) |
| IPv4 index | 13 |
| IPv6 index | 13 |
| Group joined | `ff02::fb` on interface index 13 |
| Traffic source | Real devices on the client network; an iPhone Print sheet was opened to prompt queries |

Socket options read back from the OS before listening: bound to `[::]:5353`,
`DualMode` false, `ExclusiveAddressUse` false, `SO_REUSEADDR` true,
`IPV6_PKTINFO` true, `IPV6_MULTICAST_HOPS` 255.

## Result

| Run | Instrument | Duration | Datagrams | Arrival index | Destination |
| --- | --- | --- | --- | --- | --- |
| 1 | uncommitted prototype | 120 s | 5 | 13 (all) | `ff02::fb` (all) |
| 2 | uncommitted prototype | 120 s | 38 | 13 (all) | `ff02::fb` (all) |
| 3 | `SecretPrinter.Listen6` | 60 s | 19 | 13 (all) | `ff02::fb` (all) |

**62 datagrams. Every one reported index 13. No zeros. No other index.**

Runs 1 and 2 used a prototype kept outside the repository, so that measuring
changed nothing about the repository's state. Run 3 used the same program after
it was restructured into the repository's idiom and committed as
`tools/SecretPrinter.Listen6`. The restructure changed code organisation only;
run 3 is recorded here to show the committed tool is the instrument that
produced the result, not a later rewrite of it.

## What this establishes, and exactly how far

**Established:** on this platform, `IPV6_PKTINFO` reports a non-zero interface
index, and that index equals the index of the adapter the datagram arrived on.
Arrival attribution over IPv6 can be built on it.

**Not established, and not establishable here:** whether the reported number is
the IPv6 interface numbering or the IPv4 numbering. Both families report index
13 for this adapter, so the two hypotheses predict identical observations and no
run on this hardware distinguishes them. See
[the parity finding](2026-09-13-interface-index-parity.md), which records that
no machine available to this project numbers the families differently.

`SecretPrinter.Listen6` prints both indexes and states this limitation in its
own output, so that a transcript cannot be mistaken for the stronger claim.

**Also not established:** anything about the wire. This measures what the local
socket API reports to a process on the receiving machine. No packet capture was
taken. That is a separate outstanding item, recorded in
[the unicast TTL finding](2026-09-06-unicast-ttl-gap.md).

## Incidental observations

**Two distinct queriers.** 61 datagrams came from `fe80::10e9:951e:7c37:952a`
and one from `fe80::ea9:ba0f:1890:48a9`, both link-local on scope `%13`. More
than one device on the client network speaks IPv6 mDNS. Worth knowing when the
responder's `QueriesSeen` and `IgnoredNotOurs` counters are first read against
real traffic: the volume will not all be the iPhone.

**No legacy unicast querier appeared over IPv6.** Every source port in all three
runs was 5353. REQ-ADV-017 identifies a legacy unicast querier by a source port
other than 5353, and `MdnsDatagram.IsLegacyUnicastQuerier` is family-agnostic,
so it should behave identically over IPv6 — but that is symmetry, not
measurement. The IPv6 legacy-unicast path remains unexercised, and should not be
described as verified.

**A possible discriminator for the unmeasurable claim, noted as reasoning only.**
The source addresses arrive carrying a scope zone, `%13`. A scope zone on a
link-local IPv6 address is an IPv6 concept and cannot be an IPv4 interface
index. On hardware where the two families were numbered differently, comparing
`PacketInformation.Interface` against the source address's `ScopeId` would
separate the two hypotheses. Here both are 13, so it shows nothing. Recorded in
case divergent hardware ever appears; it is an argument, not a result.

## Decision recorded: IPv6 MulticastLoopback stays at the OS default

`IPV6_MULTICAST_LOOP` was deliberately left unset on 2026-09-06, with the
reasoning recorded in the `MdnsSocket.cs` header: nothing read the IPv6 socket,
so the setting had no observable effect, and choosing it then would have been an
unmeasured choice. It was deferred to the step that adds receive.

That step is now next, so the decision comes due. **It stays at the OS default.**
Decided by Edwin West, 2026-09-13.

The reasoning: the IPv4 path already receives its own multicast back, and
`MdnsResponder.HandleAsync` discards it at the `query.IsResponse` check, which
is existing, tested behaviour. Setting the IPv6 socket's loopback to false would
make the two families behave differently for no measured reason, and would
replace a filter that is exercised by tests with a socket option that is not.
`tools/SecretPrinter.Respond6` set it false because it received on the socket it
sent from and did not want its own announcements back; the product discards them
one layer up instead.

This is a decision, not a measurement. `SecretPrinter.Listen6` never transmits,
so no loopback traffic occurred in any run above and none was observed. The
effect becomes measurable when the service's receive path exists and the service
announces: loopback arrivals will then appear as datagrams from this host's own
address and be counted. If that count is surprising, this decision is the first
thing to revisit.

## What happens next

1. A README clarification to REQ-ADV-018, stating which transport answers a
   query — the arrival transport, per the decision of 2026-09-13. The
   requirement currently specifies the records but not the transport. The spec
   change lands before the code that satisfies it, so the code is checked
   against a stated requirement rather than the two being written to match.
2. The receive path and the `MdnsResponder` family-keying fix, as one commit,
   because neither is safe alone. That earns REQ-ADV-018 and REQ-ADV-020
   together and takes binding gaps from 7 to 5.
