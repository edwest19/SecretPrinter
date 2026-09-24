# IPv4 mDNS from behind the upstairs access point does not reach FIOS-STB-01; IPv6 does

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-24. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01 and the dev box, 2026-09-24. For ten minutes,
a packet capture taken at FIOS-STB-01's network adapters, below the firewall,
held none of the IPv4 mDNS sent by the iPhone, an iPad or the dev box. All
three reach the client network through an access point upstairs, and the dev
box, behind that same access point, received every one of those packets. The
same capture holds the iPhone's and the iPad's IPv6 mDNS, and it holds the
service answering IPv4 queries from the router. This answers the open question
in the [2026-09-23 finding](2026-09-23-the-goodbye-and-ipv4-on-the-wire.md): the
iPhone's IPv4 questions went unanswered because they never reached the machine.
Where on the path IPv4 multicast is lost is not established. Nothing in the
service was changed.**

## The network

As Edwin describes it:

- The Verizon Fios router is in the basement, where the fiber comes in. Its
  Wi-Fi radio is off, and there are no extenders.
- FIOS-STB-01 is in the basement, with `Ethernet` (`192.168.1.161`) plugged
  directly into the router.
- Upstairs, a Netgear N450 is set up as an access point and connected to the
  router by Cat5. Every wireless device on the client network joins through it,
  the iPhone included.
- The dev box is upstairs, with `Ethernet 2` (`192.168.1.234`) wired into the
  N450. The two machines therefore cannot swap router ports.

The devices this finding names:

- **The iPhone, `192.168.1.152`.** Edwin read the address on the phone on
  2026-09-24. FIOS-STB-01's neighbour table maps `192.168.1.152` to the same
  hardware address as the IPv6 packets attributed to the iPhone below.
- **An iPad, `192.168.1.154`.** Its own mDNS announcement, captured below,
  carries `A 192.168.1.154`.
- The other senders in the tables, `192.168.1.1` (the router), `.2`, `.3`,
  `.155` and `.207`, are not identified further here. Which side of the access
  point `.2`, `.3`, `.155` and `.207` are on is not known.

All commands were run by Edwin, and the outputs quoted are his pastes. The
capture file and both machines' full outputs stay on those machines, outside
the repository, because they name household devices and carry hardware and
global IPv6 addresses. Everything this finding rests on is quoted or counted
here, without those.

## Before the capture: the service's counts reversed

The run that began at 23:22:58Z on 2026-09-23 ended when Edwin restarted the
service at 05:24:10Z. Its counts cover that run, less any time it spent
withdrawn:

```
Served 11 quer(ies) of 1449 seen; 1438 were for other services and were ignored.
By transport: IPv4 answered 11 of 704 seen; IPv6 answered 0 of 745 seen.
```

The lines add up (704 + 745 = 1449; 11 + 1438 = 1449). They are the reverse of
the run recorded on 2026-09-23 (`IPv4 answered 0 of 37 seen; IPv6 answered 21 of
444 seen`), on the same build and machine. The counters do not record where a
query came from, so they cannot say whose IPv4 queries were answered.

## A run that proved nothing: `Listen` on FIOS-STB-01

`tools/SecretPrinter.Listen` was run on FIOS-STB-01, joined on `Ethernet`, from
05:24:30Z. It printed only FIOS-STB-01's own packets: four lines at 05:25:02.6Z,
a query and reply about the machine's own name. It was stopped by hand, with no
summary printed.

It could not have printed anything else. `Listen` runs under `dotnet.exe`. On
FIOS-STB-01 both connections are in the Private category, the Private profile
is enabled with `DefaultInboundAction : NotConfigured` (Windows' default, which
blocks inbound traffic no rule allows), and every inbound rule on UDP 5353
names a specific program:

- `mDNS (UDP-In)`, Windows' own, for `svchost.exe`, in each profile;
- `Microsoft Edge (mDNS-In)`, for Edge and each installed WebView2 version;
- `Microsoft Copilot (mDNS-In)` and `Google Chrome (mDNS-In)`;
- an NVIDIA component;
- `SecretPrinter mDNS`, Private, for
  `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`.

None covers `dotnet.exe`, and none covers every program. Nothing from this run
is used as evidence about the network.

## The capture

To see what reaches the adapters regardless of firewall rules, the built-in
Packet Monitor was used, as in the
[2026-09-22 finding](2026-09-22-connections-to-the-printer-leave-by-the-default-route.md).
`pktmon filter list` showed no filters beforehand. Then, on FIOS-STB-01:

```
pktmon filter add SP-mDNS -t UDP -p 5353
pktmon start --capture --comp nics --pkt-size 0 -f $env:USERPROFILE\sp-mdns.etl
pktmon stop
pktmon etl2txt $env:USERPROFILE\sp-mdns.etl --verbose 2
```

`pktmon start` reported one filter, `SP-mDNS`, UDP, port 5353. `pktmon stop`
reported no events lost, and the text holds 3170 formatted events. Its first
and last times, as printed, are 02:05:29.572 and 02:19:29.747. Read as local
time (UTC−4), as the 2026-09-22 finding found for `pktmon`, that is 06:05:29Z
to 06:19:29Z. The time zone is not established, but the capture starting
before the dev box's run, below, matches the order the two were started in.

While it ran, `Listen` ran on the dev box, joined on `Ethernet 2`, from
06:05:49Z for its full 600 seconds:

```
dotnet .\tools\SecretPrinter.Listen\bin\Release\net10.0\SecretPrinter.Listen.dll --interface 192.168.1.234 --duration 600
```

The dev box shows the iPhone asking over IPv4 for `A secretprinter.local` and
`_universal._sub._ipp._tcp.local` from 06:06:31.8Z, after both captures had
started.

### IPv4

Every IPv4 sender on port 5353 in each capture. The dev box's numbers are from
its summary. FIOS-STB-01's are counts of the formatted lines of the form
`<address>.5353 > `.

| Sender | Dev box received | FIOS-STB-01's adapters recorded |
|---|---:|---:|
| `192.168.1.1`, the router | 2124 | 72 |
| `192.168.1.152`, the iPhone, behind the N450 | 36 | **0** |
| `192.168.1.161`, FIOS-STB-01 itself | 36 | 36 |
| `192.168.1.3` | 36 | 36 |
| `192.168.1.2` | 36 | **0** |
| `192.168.1.154`, the iPad, behind the N450 | 8 | **0** |
| `192.168.1.155` | 5 | 5 |
| `192.168.1.207` | 1 | **0** |
| `192.168.1.234`, the dev box, behind the N450 | 1 | **0** |

FIOS-STB-01's column also held 9 packets from `192.168.12.186`, its own
printer-side address; see "Also recorded".

Read with the UTC−4 offset above, the capture's window contains the dev box's,
so a zero in FIOS-STB-01's column is a zero over at least the same ten minutes.
Where both machines saw a sender in full (`.161`, `.3`, `.155`) the counts are
equal, which indicates `pktmon` recorded each of these packets once.

From the router, the dev box saw a burst every 10 seconds from 06:06:35Z: two
queries (`PTR _services._dns-sd._udp.local`, asked `QU`, and
`PTR 251.0.0.224.in-addr.arpa`), then about 60 more for other service types a
second later. The router lines FIOS-STB-01 recorded that were shown are of the
first two kinds, and 72 is two for each of 36 bursts. That fits the first two
of every burst arriving and the rest not; it was not checked line by line.

### The service answered the router over IPv4

Each of the 36 packets FIOS-STB-01 sent decodes as:

```
192.168.1.161.5353 > 224.0.0.251.5353: 0*- [0q] 1/0/0 _services._dns-sd._udp.local. PTR _ipp._tcp.local. (69)
```

That is SecretPrinter's service-type record. Its 69 bytes are what the
service's writer produces, since it never compresses names (`DnsMessage.cs`);
compressed, the same record would be 64. The dev box saw each of the 36 within
the same tenth of a second as a router query. The service received IPv4
queries from the router and answered them over IPv4: the IPv4 half of
`REQ-ADV-018`, working on this hardware, for queries that arrive.

### IPv6

The capture holds 69 lines to `ff02::fb`. Among them:

- **The iPad's own announcement,** sent over IPv6, which carries
  `A 192.168.1.154`.
- **Queries from the iPhone's link-local IPv6 address,** for
  `_companion-link._tcp.local` and `_rdlink._tcp.local`. Their DNS messages are
  58, 83 and 129 bytes long, in that order. Those are the sizes, in that order,
  of the IPv4 queries the dev box saw from `192.168.1.152` at 06:06:20.7Z,
  06:06:21.0Z and 06:06:22.1Z. Their source hardware address is the one
  FIOS-STB-01's neighbour table maps to `192.168.1.152`.

The iPhone sent each of these queries twice, once over each transport. The
IPv6 copies reached FIOS-STB-01; the IPv4 copies did not.

The IPv6 lines were counted and sampled, not compared sender by sender with the
dev box, whose `Listen` does not listen on IPv6.

## What this establishes

- In this window, none of the IPv4 mDNS sent by the three devices known to be
  behind the N450 (the iPhone, the iPad and the dev box) reached FIOS-STB-01's
  adapters. The dev box, behind the same N450, received all of it.
- In the same window, IPv6 mDNS from the iPhone and the iPad did reach
  FIOS-STB-01.
- The service received IPv4 queries from the router and answered them over
  IPv4.
- So the unanswered IPv4 queries recorded on 2026-09-23 are explained, for this
  window, by their not arriving. The service did not fail to answer them.
- The iPhone's address, `192.168.1.152`, is confirmed.

## What it does not establish

- **Where IPv4 multicast is lost:** in the N450, on the Cat5 link, or in how the
  router forwards what arrives from it onto FIOS-STB-01's port. Neither
  device's configuration was examined, and the machines cannot swap ports.
- **Why.** A device that forwards IPv4 multicast only toward ports where it has
  seen group membership, and passes IPv6 multicast freely, would behave like
  this. That is a known kind of behaviour, not something observed here.
- **Why only 72 of the router's packets reached FIOS-STB-01,** which is plugged
  into the router directly. The access-point picture does not explain this.
- **Which side `.2`, `.3`, `.155` and `.207` are on.** If `.3` or `.155` is
  behind the N450, their IPv4 got through, and the picture needs more than an
  IPv4 filter.
- **Whether it holds at other times.** The service's counts differ between runs,
  and one ten-minute window does not say how this varies.

## What this means for earlier findings

The claim that iOS asks only over IPv6 began with captures taken on
FIOS-STB-01 on 2026-09-06 ([finding](2026-09-06-ipv6-mdns-transport.md)).
FIOS-STB-01 is the vantage point that, in this capture's window, did not
receive IPv4 multicast from behind the N450, where the phone is. Seeing no IPv4 from the
phone there fits this path, rather than a preference in iOS.

Two findings were corrected on 2026-09-23. The places that still carry the
claim are listed in that day's finding under "The same claim, not corrected
here"; none is changed by this one. A dated note is added to the 2026-09-23
finding pointing here.

## Also recorded, not chased

- **The printer side dropped again.** After the 05:24:10Z restart the printer
  did not answer at startup. The service logged at 05:24:16Z that `Wi-Fi` was
  up, held `192.168.12.186`, and that the address was preferred. Later the
  Broadcom `Wi-Fi` was found `Disconnected`. Edwin reconnected it with
  `netsh wlan connect`, `Test-NetConnection 192.168.12.180 -Port 631`
  succeeded over `Wi-Fi`, and after a restart the service announced at
  06:01:07Z. When the adapter disconnected is not known. `netsh wlan show
  interfaces` then showed it on 802.11n, channel 3 (2.4 GHz), at 80% signal.
- **The printer side in the capture.** It holds 9 packets from
  `192.168.12.186` (the service asking the printer) and none from the printer's
  address. How `pktmon` renders what arrives on a Wi-Fi adapter was not
  checked, so nothing is drawn from this.
- **The iPhone asked for `epson3ea18a.local`,** the printer's host name on the
  printer network, several times between 05:22Z and 05:27Z. The advertisement
  never contains that name. How the phone learned it is not established. It may
  bear on the
  [second printer entry](2026-09-21-a-second-printer-entry-on-the-iphone.md).
- **FIOS-STB-01 announced its own name dozens of times** from 05:22:42.9Z, as
  seen from the dev box. Why is not known.
- **`Listen`'s README says nothing about the firewall.** On a machine where no
  inbound rule covers `dotnet.exe`, it receives only that machine's own
  packets, as above. Not changed here.

## Prediction record

Claude predicted:

- Before the service log was read, that the 05:24:09.7Z goodbye came from a
  withdrawal. **Did not hold:** Edwin had restarted the service.
- That no firewall rule would cover `dotnet.exe` and the Private default would
  be `NotConfigured`. **Held,** with more named programs than predicted.
- For the capture:
  - one active filter and no events lost. **Held.**
  - IPv4 from wired hosts such as the dev box would reach FIOS-STB-01. **Did not
    hold:** the dev box's did not.
  - the iPhone's IPv6 queries would reach FIOS-STB-01. **Held.**
  - none of the iPhone's IPv4 queries would. **Held.**
- That FIOS-STB-01's replies would decode as one `PTR _ipp._tcp.local.` answer
  of 69 bytes. **Held.**
- That the neighbour table would map `192.168.1.152` to the IPv6 packets'
  source address. **Held.**
