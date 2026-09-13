# SecretPrinter.Listen6

A receive-only diagnostic, the IPv6 counterpart of
[`SecretPrinter.Listen`](../SecretPrinter.Listen/README.md). It answers one
question that the service's IPv6 support depends on.

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-13. Reviewed by a human before merge.*

## The question

> When a datagram arrives on a socket bound to `[::]:5353` with `IPV6_PKTINFO`
> enabled, does the operating system report a non-zero interface index naming
> the adapter it arrived on?

REQ-ADV-020 requires the service to determine the arrival interface of an IPv6
query from `IPV6_PKTINFO` rather than inferring it. Before this tool was run,
the option had been set and read back, which proves only that Windows accepted
it. Nobody had observed it reporting anything usable. Writing the receive path
first and discovering the answer afterwards would have risked a late rewrite of
the whole attribution design.

## What it does, completely

1. Finds the adapter holding the IPv4 address you name, and reads both its IPv4
   and IPv6 interface indexes.
2. Opens one UDP socket on `[::]:5353` with `SO_REUSEADDR` and
   `ExclusiveAddressUse = false`, so it *shares* the port with `Dnscache` rather
   than seizing it, and with `IPV6_V6ONLY` set so it does not also receive IPv4.
3. Enables `IPV6_PKTINFO` and joins `ff02::fb` on that interface index.
4. Reads back every option from the OS and prints them.
5. Prints one line per datagram — arrival index, destination address, source,
   byte count — then a summary.

## What it does not do

- It does **not** transmit. Not a query, not an announcement, not a reply. It
  joins a multicast group and reads.
- It does **not** write files, touch the registry, or alter firewall rules.
- It does **not** need elevation.
- It does **not** modify or interact with `SecretPrinter.Service`.

## What its output cannot be read as proving

The reported index could be the IPv6 interface numbering or the IPv4 numbering.
On every adapter this project has measured, both families report the same index
for a given adapter, so no run on that hardware can distinguish the two. The
program prints both indexes and says this in its own output, so a transcript
cannot be mistaken for the stronger claim. See
[the parity finding](../../docs/findings/2026-09-13-interface-index-parity.md).

## Privacy

mDNS is chatty and names devices. This tool prints source addresses and byte
counts for every mDNS datagram on the link — phones, televisions, computers, not
only printers. It holds nothing on disk. **If you publish a transcript, read it
first.**

## Usage

```
dotnet run --project tools/SecretPrinter.Listen6 -- --ipv4 192.168.1.234 --duration 120
```

`--ipv4` names the interface to join on, by an address held by that adapter.
`--duration` is in seconds and defaults to 120.

To generate traffic, use a device on that network that queries over IPv6 — on
iOS, opening the Print sheet is enough.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Datagrams were received and summarised |
| 1 | Nothing arrived. **Not** a negative result for `IPV6_PKTINFO` — suspect the firewall or an idle link |
| 2 | Argument or adapter problem |
| 3 | Socket error, with the `SocketError` code printed |

## Result

Recorded in
[`docs/findings/2026-09-13-ipv6-pktinfo-arrival.md`](../../docs/findings/2026-09-13-ipv6-pktinfo-arrival.md).

## Why this is not scheduled for deletion

`SecretPrinter.Respond6` stands in for behaviour the product will have, and is
deleted once the product has it. This tool measures a property of the operating
system instead, which stays worth re-checking on a new machine, a new Windows
build, or a new adapter.
