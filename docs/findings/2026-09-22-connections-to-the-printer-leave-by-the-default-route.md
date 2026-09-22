# Connections to the printer leave by the default route when the printer side is down

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01. With no link on the printer side, a TCP
connection to the printer's address left the machine on the client-side
Ethernet and was handed to the client network's router. Five SYNs went out; no
reply came back. The connection was made by `Test-NetConnection`, standing in
for the service's socket; see "What this does not establish". Nothing is fixed
here. The README now discloses it (see "Decided"); the code is unchanged.**

## Why this was measured

[`2026-09-17-connect-timeout-is-not-reported.md`](2026-09-17-connect-timeout-is-not-reported.md)
left a hypothesis in its section "What this does not establish": during the
2026-09-21 WLAN drop, ten connections to `192.168.12.180` were attempted after
the printer-side link had gone, and Windows may have sent their SYNs out the
default route, onto the client network. That is a question about confinement,
so it was measured before anything else.

## What the code does

`TcpConnectionFactory.ConnectAsync` (`src/SecretPrinter.Proxy/Connections.cs`)
creates `new TcpClient()` and connects it without binding a local address. The
socket carries no interface and no source address of its own, so the choice of
where its packets leave is made by the Windows routing table alone. This is read
from the code. The listener is different: it binds one specific address.

No requirement in the README says the connection to the printer leaves by the
printer-side interface. `REQ-RES-006` confines *resolution queries* to that
interface; `REQ-RES-001` and `REQ-RES-007` say where the printer's address comes
from. So no requirement is contradicted. But `README.md` and `docs/operating.md`
both say the service confines itself to printing, and a reader would reasonably
take that to include the path its connections take. This finding exists so that
reader is not misled.

## The measurement

FIOS-STB-01, Windows 10 build 19045. Client side: `Ethernet`, index 15,
`192.168.1.161`, default gateway `192.168.1.1`. Printer side: the Broadcom
802.11n adapter (`Wi-Fi`, index 9), which had already dropped and was
`Disconnected`. All commands were run by Edwin; the outputs quoted are his
pastes. Timestamps are as `pktmon` printed them; their time zone was not
established.

### A second printer-side adapter was connected

The first route query found something the previous handoff did not record: the
Compact Wireless-G USB adapter (`Wi-Fi 2`, index 49) was `Up` on the printer
network with the DHCP address `192.168.12.136`. Asked for the route to the
printer, Windows chose it:

```
Find-NetRoute -RemoteIPAddress 192.168.12.180
  IPAddress         : 192.168.12.136   (InterfaceAlias Wi-Fi 2, index 49)
  DestinationPrefix : 192.168.12.0/24
  NextHop           : 0.0.0.0
  InterfaceMetric   : 55
```

That is a separate observation, and it is **a routing decision only; no packets
were captured in this state.** It says that with the configured printer-side
adapter down and another adapter on the printer's subnet up, an unbound
connection would leave by the adapter the configuration does not name. It stays
on the printer network, but not on the configured interface. Whether both
adapters being up at once would make the choice depend on interface metrics was
not measured; the Broadcom's metric was not read.

### With no printer-side link, the route is the default route

`Wi-Fi 2` was disconnected with `netsh wlan disconnect interface="Wi-Fi 2"`.
Five seconds later both Wi-Fi adapters reported `Disconnected`, and:

```
Find-NetRoute -RemoteIPAddress 192.168.12.180
  IPAddress         : 192.168.1.161    (InterfaceAlias Ethernet, index 15)
  DestinationPrefix : 0.0.0.0/0
  NextHop           : 192.168.1.1
  InterfaceMetric   : 25
```

### The packets

A capture was taken at the network adapters with the built-in Packet Monitor,
filtered to TCP to or from `192.168.12.180` on port 631:

```
pktmon filter add SP-Printer631 -i 192.168.12.180 -t TCP -p 631
pktmon start --capture --comp nics --pkt-size 0 -f sp-route.etl
Test-NetConnection -ComputerName 192.168.12.180 -Port 631 -InformationLevel Detailed
pktmon stop
```

`pktmon start` reported exactly one active filter, `SP-Printer631`, and
`pktmon stop` reported no events lost. `Test-NetConnection` reported
`InterfaceAlias : Ethernet`, `SourceAddress : 192.168.1.161`,
`NetRoute (NextHop) : 192.168.1.1`, `TcpTestSucceeded : False`.

Formatted with `pktmon etl2txt --verbose 2`, the capture holds five packets, all
transmitted on component 12, the Broadcom NetLink Gigabit Ethernet miniport,
IfIndex 15:

| Time (as printed) | Packet |
|---|---|
| 10:47:42.498 | `192.168.1.161.1199 > 192.168.12.180.631: Flags [S], seq 498509534, … length 0` |
| 10:47:43.502 | same SYN, retransmitted |
| 10:47:45.509 | same SYN, retransmitted |
| 10:47:49.509 | same SYN, retransmitted |
| 10:47:57.524 | same SYN, retransmitted |

All five carry the same sequence number, so they are one connection attempt:
one SYN and four retransmissions, 1, 2, 4 and 8 seconds apart. The destination
MAC on every packet is the one `Get-NetNeighbor` maps to `192.168.1.1` on
`Ethernet`: the client network's router.

The adapter counters agree independently. Component 12 counted
`Packets Out 5, Bytes Out 330` (5 × 66 bytes). Every other component counted 0,
including both Wi-Fi miniports (components 2 and 3) and every filter stacked on
them. No component counted a packet received: no SYN-ACK, no reset, nothing
from the router.

The adapter MACs, and the machine's global IPv6 addresses that `pktmon` lists
among its components, are deliberately left out of this file. The raw capture
was not committed.

## What this establishes

- With no link on the printer side, Windows routes a connection to the
  printer's address out the client-side Ethernet, from the client-side address,
  to the client network's router.
- It sends SYNs that way, on the wire. The router answered none of them.
- The service's outbound socket is unbound, so it is subject to the same
  routing decision.

## What this does not establish

- **That the service's own socket behaved this way.** The connection was made
  by `Test-NetConnection`, not by SecretPrinter. The service could not be used:
  with the printer side down it withdraws and closes its listener, so no job
  reaches the connect. The 2026-09-21 connections happened in the short window
  between the drop and the withdrawal, when lookups were still answered from
  cache. Both sockets are unbound TCP connects through the same stack, and the
  capture's source address matches the route the service's socket would get,
  but a capture of the service itself was not taken.
- **How many SYNs a service job sends.** The service's connect deadline is
  `PrinterConnectTimeout`, 10 seconds by default. With the backoff measured here
  (0, 1, 3, 7 s), that would be four SYNs per job before the deadline fires.
  That is arithmetic on this measurement, not a measurement of the service.
- **What the router did with them.** The capture is on FIOS-STB-01. Whether the
  router dropped the SYNs or forwarded them toward its own upstream was not
  observed.
- **The two-adapter case.** See above: a route query, no capture.

## Prediction record

Before the capture, Claude predicted three SYNs spaced about 3 s and then 6 s,
calling it Windows' default "as far as I know". The measurement was five SYNs
spaced 1, 2, 4 and 8 s. The prediction was wrong, and is recorded here rather
than dropped. Claude also predicted, before the first route query, that it
would return the default route; it returned the on-link route through
`Wi-Fi 2`, because a second printer-side adapter was connected that Claude had
not asked about.

## What goes with the job data

Nothing. No connection completed, so no TLS handshake began and no IPP bytes
were sent. The relay writes job data only after a TLS handshake with the pinned
printer succeeds, and that cannot happen against an address that does not
answer. What leaves is the SYN itself: the printer's address and port, from the
client-side address, onto the client network.

## Decided

- **The README states it.** Decided by Edwin West on 2026-09-22: the README says
  plainly that the connection to the printer follows the Windows routing table
  and is not bound to the printer-side interface. It is the fifth item under
  "Read this before installing", and Section 3 repeats it against step 5 of the
  architecture. The code is unchanged.

## Open

- **`REQ-SEC-008`.** It forbids transmitting to any host outside the two
  configured networks. The SYNs measured here were addressed to the printer
  network and handed to the client network's router, a host on a configured
  network, so nothing seen here breaks it. Whether the router forwarded them
  further was not observed.
- **Whether the code can confine it.** Not evaluated. In particular, whether
  binding the outbound socket to the printer-side address confines its egress on
  Windows, and what happens when that address has gone with the link, have not
  been measured, and nothing here should be read as saying they would.
- **The 2026-09-17 finding** still states this as a hypothesis. It should point
  here.
