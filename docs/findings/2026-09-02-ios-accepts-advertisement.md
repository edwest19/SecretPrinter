# Finding: iOS accepts a proxy advertisement, without IPPS

**Date:** 2026-09-02
**Tool:** `tools/SecretPrinter.Respond`
**Recorded by:** Claude (Anthropic model, Claude Opus 4.5), directed by Edwin West

This run closed the last two unresolved technical questions in the design. It is
the point at which the proxy architecture stopped being a plan and became
something measured.

## Questions being answered

1. Can the process **send** mDNS responses that clients accept, while the
   Windows DNS Client service holds port 5353?
2. Will iOS offer a printer advertised **only** as `_ipp._tcp`, with no IPPS
   variant?

## Method

```
dotnet run --project tools/SecretPrinter.Respond -- \
  --interface 192.168.1.234 --duration 300
```

Advertised one fake printer on the Optimum network only, with an instance name
of `SecretPrinter TEST - do not use` and no IPPS service. Nothing was listening
on the advertised port. An iPhone on that network then opened a print dialog.

## Result

**The fake printer appeared in the iOS printer list.** Both questions answered
yes.

```
Bound 0.0.0.0:5353 (SO_REUSEADDR). Joined 224.0.0.251 on 192.168.1.234 (index 13).
  announce 1/3  796 bytes to 224.0.0.251:5353
[  27.7s] answered 192.168.1.41    multicast  663B  Ptr _universal._sub._ipp._tcp.local
[  28.7s] answered 192.168.1.41    multicast  663B  Ptr _universal._sub._ipp._tcp.local
[  31.7s] answered 192.168.1.41    multicast  663B  Ptr _universal._sub._ipp._tcp.local
[  65.6s] answered 192.168.1.41    multicast  663B  Ptr _universal._sub._ipp._tcp.local
[  66.6s] answered 192.168.1.41    multicast  663B  Ptr _universal._sub._ipp._tcp.local
```

| Measure | Value |
| --- | --- |
| Queries seen on our interface | 17 |
| Queries answered | 5 |
| Dropped, wrong interface | 0 |
| Unparseable packets | 0 |
| Goodbye records sent | yes |

## The most important observation

**Every query iOS sent was for the subtype, never for the base service type.**

All five answered queries were `_universal._sub._ipp._tcp.local`. The client did
not once ask for plain `_ipp._tcp.local`.

A responder advertising only the base service type would therefore never be
discovered by iOS at all. `REQ-ADV-002` is not a refinement — it is the entire
discovery path, and this run is the evidence.

## Second observation: IPPS is not required for discovery

The advertisement contained no `_ipps._tcp` service and no TLS. iOS listed the
printer anyway.

This removes TLS termination from the critical path. The proxy does not need to
implement IPPS to be discovered.

**Scope of that claim, stated precisely:** this proves *discovery* without IPPS.
It does not prove that a print job will complete over plain IPP, because nothing
was listening on the advertised port and no job was ever sent. That remains
untested until the relay exists.

## Third observation: interface confinement worked

`Dropped, wrong interface: 0`, and no query from the T-Mobile network was ever
answered. The advertisement stayed on the network it was aimed at, which is the
behaviour REQ-ADV-011 requires.

Of 17 queries seen on the Optimum interface, only 5 were about us. The other 12
were ordinary mDNS traffic from other devices and were ignored, as REQ-ADV-012
requires.

## Departures from RFC 6762 in this run

The tool does **not** probe for name conflicts before claiming its name (§8.1).
It ran briefly and under supervision, with a name nothing else would plausibly
hold. **The service must probe.** Recorded here so the omission is not later
mistaken for the intended design.

## Consequences for the design

- The proxy architecture is viable end to end for discovery.
- `_universal._sub._ipp._tcp.local` must be advertised, or nothing works.
- IPPS is deferred, not required.
- Multicast responses were accepted; no unicast-response handling was needed for
  this client, though the tool implements it.
