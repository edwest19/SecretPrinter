# iOS accepts an A record delivered over IPv6 mDNS transport

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West. Reviewed by a human before merge.*

**Date:** 2026-09-06
**Status:** Concluded. Result positive, reproducible.
**Instrument:** `tools/SecretPrinter.Respond6`

---

## Question

> Will iOS accept an `A` record (IPv4 only, no `AAAA` available) delivered over
> **IPv6** mDNS transport, and then open an IPP connection over **IPv4** to
> port 631?

The Epson ET-3760 is IPv4-only, so the answer determined how much had to change:

| If | Then |
| --- | --- |
| Yes | IPv6 is an additional transport inside `SecretPrinter.Mdns`. Advertising, Resolution and Proxy are unaffected. |
| No | IPv6 must reach into the proxy and the SRV/TXT layers, because the printer cannot be reached over IPv6. |

## Why the question arose

Capture on `FIOS-STB-01`, interface `Ethernet 3`, filter
`tcp port 631 or udp port 5353`, 2026-09-06 04:01:08–04:02:35 UTC, 28 packets:

- Every packet originated from the iPhone at `192.168.1.152`, sent from an IPv6
  link-local address to `ff02::fb`.
- **Zero IPv4 mDNS packets appeared on the segment.**
- **Zero responses of any kind were sent.** No packet originated from that
  interface's own hardware address.
- The phone queried `_universal._sub._ipp._tcp.local`, and `A`, `AAAA` and
  `HTTPS` for `secretprinter.local`.

`netstat -ano | findstr 5353` showed `SecretPrinter.Service` bound to
`0.0.0.0:5353` and to nothing on `[::]`.

**The service was not failing to answer. It never received the queries.** It had
no IPv6 socket, and every query on that segment was IPv6.

A separate defect was found and corrected first: two NICs on the host held
addresses on the same `192.168.1.0/24` with identical route metrics, because
Verizon and Optimum both default to that range. Windows had no basis to choose
between them, so outbound unicast could leave on the wrong wire. The Optimum
interface was disconnected permanently.

## Method

`tools/SecretPrinter.Respond6` listens on IPv6 mDNS only, answers with an `A`
record, and returns `NSEC` asserting no `AAAA` exists so iOS receives a definite
negative rather than silence. It opens no TCP socket and never contacts the
printer.

```
dotnet run --project tools\SecretPrinter.Respond6 -- --interface "Ethernet" --ipv4 192.168.1.98
```

Conditions:

- `SecretPrinter.Service` **stopped**, so exactly one responder answered for
  these names. Confirmed in the capture: zero IPv4 mDNS responses from
  `192.168.1.98`.
- Wireshark on the same interface, filter `udp.port == 5353 || tcp.port == 631`.
- Print initiated from the iPhone at `192.168.1.152`.

| Role | Value |
| --- | --- |
| Host | `FIOS-STB-01`, Windows 10 19045 |
| Client-side interface | `Ethernet`, `192.168.1.98`, IPv6 index 15 |
| Printer-side interface | `Wi-Fi`, `192.168.12.186` |
| Client | iPhone, `192.168.1.152`, on NETGEAR51 (bridged, same broadcast domain) |
| Printer | Epson ET-3760, `192.168.12.180` (not contacted) |
| Advertised host | `secretprinter.local` → `192.168.1.98` |
| Advertised instance | `SecretPrinter (ET-3760)._ipp._tcp.local`, port 631 |

The connection was expected to fail. The measurement is whether iOS **attempts**
it.

## Observations

Capture 2026-09-06 05:06:47–05:09:02 UTC, 694 packets. Times relative to start:

```
t = 31.21   mDNS response sent over IPv6, 3 records, A = 192.168.1.98
t = 31.28   SYN -> 192.168.1.98:631 over IPv4   (sport 65240)
t = 31.28   SYN -> 192.168.1.98:631 over IPv4   (sport 65241)
t = 31.28   SYN -> 192.168.1.98:631 over IPv4   (sport 65242)
t = 31.28   SYN -> 192.168.1.98:631 over IPv4   (sport 65243)
```

**70 milliseconds** between the IPv6 mDNS response carrying the A record and the
first IPv4 SYN.

- 69 TCP packets, all to `192.168.1.98:631`, all from `192.168.1.152`, all SYN.
- 8 distinct source ports (65240–65247) across 52 seconds. Sustained retry, not
  one incidental attempt.
- 8 SYNs carried ECN flags, the rest plain. Normal iOS negotiation.
- **Zero packets returned from `192.168.1.98` on TCP.** No SYN-ACK, no RST.
- 8 mDNS responses sent, all over IPv6, all from this tool.
- Zero IPv4 mDNS from the phone, consistent with the earlier capture.

## Result

**Confirmed: yes.**

iOS accepted an `A` record delivered over IPv6 mDNS transport and opened an IPP
connection over IPv4. No `AAAA` was required. The `NSEC` assertion that no `AAAA`
exists was accepted as a definite negative and did not stall the address lookup.

## What this implies for the specification

No requirement IDs are claimed here. This section states what the README will
need to say; assigning IDs is a separate, deliberate step.

1. IPv6 mDNS transport is **required**, not optional. A client network offering
   IPv6 makes iOS query over IPv6 exclusively, and an IPv4-only responder is
   invisible there. Same character as `REQ-ADV-002`.
2. The change is scoped to transport. The `A` record may keep carrying the IPv4
   address of the proxy interface; the printer stays IPv4-only. Advertising,
   Resolution and Proxy need no IPv6 awareness.
3. `NSEC` for absent `AAAA` should be part of responder behaviour. Silence is
   measurably worse than a definite negative.
4. Binding `0.0.0.0` rather than configured interfaces, as the service currently
   does, diverges from README Section 3.1. On a multi-homed host it makes
   source-address selection an OS decision rather than a SecretPrinter decision.
   Reconcile when IPv6 is added.

These are testable against a fake transport, so they should be code-covered with
`[Requirement]` attributes rather than evidenced in `docs/verification.md`.

## Incidental findings

**No RST returned for the refused connections.** Windows normally answers a
closed port with RST. Silent drop indicates Windows Firewall discarded the SYNs.
When the real service runs, inbound TCP 631 must be permitted on the client-side
interface, or discovery will succeed and printing will still fail. A plausible
contributor to the original symptom.

**The Verizon router at `192.168.1.1` issued 590 IPv4 mDNS queries** during the
135-second capture. Whether that is its own discovery or a relay feature is
uncharacterised. A router that relays mDNS could interact with SecretPrinter in
ways not yet analysed. Flagged; not blocking.

**The phone repeatedly queried `epson3ea18a.local`**, the printer's real
hostname, and the experiment correctly ignored it. That name appeared in no
record SecretPrinter sent — the honesty constraints in README Section 5 held. It
is a stale cache entry on the phone. If iOS shows a dead "EPSON ET-3760" beside
the SecretPrinter entry, selecting the wrong one fails exactly as observed. Rule
this out before attributing a future failure to code.

## Limitations

This does **not** establish:

- That a print job completes. Nothing listened on 631; only the connection
  attempt was measured.
- That the TXT record set is correct. The experiment used plausible defaults, not
  values measured from the ET-3760. iOS attempted the connection regardless, so
  TXT sufficiency remains untested.
- Behaviour on any other iOS version, or on macOS, or on Android.
- Behaviour on an IPv6-only client network. Here the client had both and chose
  IPv6 for mDNS, IPv4 for IPP.
- That `SecretPrinter.Respond6`'s wire format matches `SecretPrinter.Dns`. The
  experiment reimplements DNS encoding independently and deliberately, so a
  defect in it would say nothing about the product.

## Reproduction

1. Stop `SecretPrinter.Service`.
2. Capture on the client-side interface, filter
   `udp.port == 5353 || tcp.port == 631`.
3. Run `tools/SecretPrinter.Respond6` with `--interface` and `--ipv4` for that
   interface.
4. Print from an iOS device on the same broadcast domain.
5. Look for a SYN to `<interface address>:631`. A refused or dropped connection
   still counts as positive.

## Evidence handling

The captures contain hardware addresses and link-local identifiers for devices on
a private network. They are **deliberately not committed.** Observations above
are transcribed from them; hardware addresses and IPv6 interface identifiers are
omitted, and the RFC 1918 addresses retained are those needed to reproduce.

Captures are retained locally by the maintainer and available on request for
independent verification.
