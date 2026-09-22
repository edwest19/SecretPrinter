# Connections to the printer leave by the default route when the printer side is down

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

*Corrected later on 2026-09-22 by Claude (Anthropic model, Claude Opus 5) at
the direction of Edwin West. As first committed, this file said the configured
printer-side adapter was the Broadcom (`Wi-Fi`, index 9), and described the
route through the Compact Wireless-G (`Wi-Fi 2`, index 49) as a connection
leaving by "the adapter the configuration does not name". That was wrong:
`Wi-Fi 2` was the configured adapter. The service log's startup lines show the
configuration naming `Wi-Fi` at 2026-09-21 20:17:33Z and `Wi-Fi 2` from
21:06:55Z on, and the service withdrew 28 seconds after `Wi-Fi 2` was
disconnected this morning. Claude carried "the Broadcom is the printer side"
from the previous handoff, which was true when written, without reading the
configuration in force. The sections "The measurement", "The configured
adapter was up", "What this does not establish" and "Prediction record" are
rewritten below. The measured result - with no printer-side link, the SYNs left
on Ethernet to the router - is unchanged, because both adapters were down when
the capture ran. Reviewed by a human before merge.*

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
`192.168.1.161`, default gateway `192.168.1.1`. Printer side, as configured:
the Compact Wireless-G USB adapter (`Wi-Fi 2`, index 49), `192.168.12.136`. The
machine's other Wi-Fi adapter, the Broadcom 802.11n (`Wi-Fi`, index 9), had
been the printer side until 2026-09-21 and was `Disconnected`. All commands were
run by Edwin; the outputs quoted are his pastes. Timestamps are as `pktmon`
printed them. Their time zone was not established; they are consistent with
local time (UTC-4), because the capture at "10:47" followed the disconnect that
the WLAN event log records at 10:42:30 local.

### The configured adapter was up

The first route query was taken with `Wi-Fi 2` still connected. Windows chose
its on-link route:

```
Find-NetRoute -RemoteIPAddress 192.168.12.180
  IPAddress         : 192.168.12.136   (InterfaceAlias Wi-Fi 2, index 49)
  DestinationPrefix : 192.168.12.0/24
  NextHop           : 0.0.0.0
  InterfaceMetric   : 55
```

This is the ordinary case: the configured adapter is up, and Windows routes to
the printer through it. It is a routing decision only; no packets were captured
in this state. It does not answer the question this finding is about, which
needs no printer-side link at all.

### With no printer-side link, the route is the default route

`Wi-Fi 2` was disconnected with `netsh wlan disconnect interface="Wi-Fi 2"`.
The WLAN-AutoConfig log records it at 14:42:30Z (event 8003, *"The network is
disconnected by the user."*), and the service log records the advertisement
withdrawn and the listener closed at 14:42:58Z. Nothing reconnected the adapter
until Edwin did, by hand, at 16:10:01Z (events 8000 and 8001, *"Manual
connection with a profile"*). Five seconds after the disconnect both Wi-Fi
adapters reported `Disconnected`, and:

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
- **Two adapters on the printer's network at once.** Not observed. With both
  up, Windows would have two on-link routes to the printer's subnet and could
  choose either, so a connection could leave by an adapter the configuration
  does not name. That follows from the unbound socket and the routing table; it
  was never set up or measured here.

## Prediction record

Before the capture, Claude predicted three SYNs spaced about 3 s and then 6 s,
calling it Windows' default "as far as I know". The measurement was five SYNs
spaced 1, 2, 4 and 8 s. The prediction was wrong, and is recorded here rather
than dropped. Claude also predicted, before the first route query, that it
would return the default route; it returned the on-link route through
`Wi-Fi 2`. Claude then explained that as "a second printer-side adapter",
because it had not read the configuration: `Wi-Fi 2` was the configured
adapter, and it was up. Claude predicted the service would withdraw about a
minute after the disconnect; it withdrew after 28 seconds.

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
