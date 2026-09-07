# A unicast mDNS response probably leaves at TTL 128, not 255

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-06. Reviewed by a human before merge.*

**Status: unconfirmed.** Reasoned from the socket API, not observed. No packet
capture has been taken. Nothing has been changed in response to it.

## What was found

`MdnsSocket.SendAsync` is the single send path behind two public methods:

- `SendMulticastAsync`, which addresses `224.0.0.251:5353`
- `SendUnicastAsync`, which addresses a legacy unicast querier directly, as
  RFC 6762 §6.7 and REQ-ADV-017 require

Before every send it sets `IP_MULTICAST_TTL` to 255. That option applies only to
datagrams whose destination is a multicast address. A datagram addressed to a
unicast address takes `IP_TTL`, which nothing in this repository sets, so it
should leave the machine at the Windows default of 128.

RFC 6762 §11 asks for TTL 255 on Multicast DNS responses *including those sent
by unicast*, so that a receiver can reject anything that has been routed.
REQ-ADV-013 states it without qualification:

> Outgoing mDNS packets carry IP TTL 255, per RFC 6762 §11.

**REQ-ADV-013 currently reads `OK` in the coverage matrix.** If the reasoning
above holds, that marker claims a behaviour the service has for one of its two
send paths and not the other.

## How it was found

While adding the IPv6 send path, working out whether a `[Requirement]` marker
for REQ-ADV-019 would be honest. REQ-ADV-019 is the IPv6 counterpart of
REQ-ADV-013 and has the same wording, so the same question arose: does
`IPV6_MULTICAST_HOPS` cover a unicast reply? It does not. The IPv6 send path was
therefore written to set `IPV6_UNICAST_HOPS` as well, and then the IPv4 path was
read again and found to have no equivalent.

The requirement was written by Claude, the code was written by Claude, and the
marker asserting the code satisfies the requirement was placed by Claude. The
gap survived because every one of those three steps thought about multicast.

## What has not been done, and why

Nothing in the IPv4 path was changed. That work belongs in its own commit,
because it modifies behaviour under a requirement already marked covered, and
folding it into the IPv6 change would have hidden a correction inside a feature.

`docs/verification.md` has not been touched. REQ-ADV-013 is code-covered, not
evidence-covered, so there is no row to amend, and no requirement identifier
exists for "this marker may be wrong."

The matrix has not been changed either. Removing the REQ-ADV-013 marker on
reasoning alone would trade one unverified claim for another.

## What would settle it

A packet capture on the client-facing interface of a legacy unicast response,
reading the TTL field of the IP header. The response is triggered by an mDNS
query whose source port is not 5353; `tools/SecretPrinter.Probe` sends from an
ephemeral port and is the obvious way to produce one.

Two outcomes:

- **TTL 128.** REQ-ADV-013 is unmet. Set `IP_TTL` to 255 in `SendAsync`
  alongside the multicast option, in the same shape as the IPv6 path, and re-run
  the check.
- **TTL 255.** The reasoning here is wrong, Windows is doing something
  undocumented on a socket that has `IP_MULTICAST_TTL` set, and *that* is worth
  writing down, because the IPv6 code was built on the opposite assumption.

Until one of those has happened, this document is the whole of what is known.

## Also unresolved

The IPv6 side is now written to set both hop limits, but no capture confirms
that either value reaches the wire. `tests/SecretPrinter.Mdns.Tests` reads the
options back from the socket, which proves the operating system accepted them
and nothing more. The same capture that settles the IPv4 question should read
the hop limit on an IPv6 response while it is there.
