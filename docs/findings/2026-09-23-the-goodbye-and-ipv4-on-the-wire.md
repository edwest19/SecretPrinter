# The goodbye reaches the network, and IPv4 questions for the printer go unanswered

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-23. Reviewed by a human before merge.*

**Status: measured on the client network from a second machine, 2026-09-23.
The service's goodbye (`REQ-LIF-003`) was seen reaching another host for the
first time, with every TTL at zero, and its startup announcements were seen
the same way with the TTLs the code sets. In the same session, the device
earlier findings identify as the iPhone, and an iPad, sent IPv4 mDNS queries
for names the service advertises while it was advertising, and the service
answered no IPv4 query at all. Why is not established. Nothing in the service
was changed. Two earlier findings that said iOS asks only over IPv6 are
corrected alongside this one.**

## The setup

- **The service.** FIOS-STB-01, registered as `SecretPrinter` under
  `NT AUTHORITY\LocalService`, with the build installed on 2026-09-22
  (`523bacd`, per [that day's finding](2026-09-22-the-withdrawn-start-on-hardware.md)).
  This run did not re-check the installed files. The configuration had since
  been changed to put the Broadcom adapter on the printer side. It logged these
  lines at its 20:35:07Z start, and the same three again at 23:22:58Z:

  ```
    client interface : Ethernet -> 192.168.1.161 (index 17)
    printer adapter  : Wi-Fi (resolved when it is usable; logged below)
    printer interface: Wi-Fi -> 192.168.12.186 (index 10)
  ```

- **The observer.** The dev box, on the same client network through
  `Ethernet 2` (`192.168.1.234`, index 13), running `tools/SecretPrinter.Listen`
  as of `a136eaf`, joined to `224.0.0.251` on that adapter only. No SecretPrinter
  process was running there. `Listen` is receive-only and IPv4-only. It sees
  multicast, not unicast, and it does not print source ports. Its full output
  was also written to files kept on the dev box, outside the repository,
  because they name household devices; every line this finding rests on is
  quoted here.
- **The clients.** The device at `192.168.1.152`, which earlier findings
  identify as the iPhone (not re-checked in this run), and a device at
  `192.168.1.154` whose own mDNS records describe it as an iPad.
- **Clocks.** Each machine logs its own UTC, and the two were not compared.
  The goodbye below shows the dev box's clock at least half a second behind
  FIOS-STB-01's, so lines from the two machines are matched by order and to
  the second, no closer.

All commands were run by Edwin, and the outputs quoted are his pastes.

## Run 1: IPv4 questions that nothing answered

`Listen` ran on the dev box from 21:29:18Z for 1800 seconds:

```
dotnet .\tools\SecretPrinter.Listen\bin\Release\net10.0\SecretPrinter.Listen.dll --interface 192.168.1.234 --duration 1800
```

It received 190 packets from 6 sources: 156 queries and 34 responses, none
unparseable.

The service was advertising throughout. Its log records no withdrawal from
20:36Z until 22:03:07Z, and no restore before 23:12:23Z. Inside this window it
accepted and relayed connections from `.152` between 21:30:03Z and 21:31:47Z,
and from `.154` between 21:32:09Z and 21:32:30Z.

In the same minutes both devices asked over IPv4 for names the service
advertises. Each of these 15 queries carries at least one question the
advertisement answers: the PTR for `_universal._sub._ipp._tcp.local` in 14 of
them, and the A record for `secretprinter.local` in the first (dev-box clock):

```
[21:30:02.6Z] if=13  Ethernet 2     from 192.168.1.152      49B  QUERY  65 secretprinter.local, Aaaa secretprinter.local, A secretprinter.local
[21:30:03.8Z] if=13  Ethernet 2     from 192.168.1.152      77B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:30:04.8Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:30:40.2Z] if=13  Ethernet 2     from 192.168.1.152     135B  QUERY  Ptr _universal._sub._ipps._tcp.local, 65 secretprinter.local, Ptr _universal._sub._ipp....
[21:30:41.2Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:30:44.2Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:31:14.9Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:31:15.9Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:31:22.1Z] if=13  Ethernet 2     from 192.168.1.152     135B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local, 65 secretpri...
[21:31:23.1Z] if=13  Ethernet 2     from 192.168.1.152     115B  QUERY  Ptr _universal._sub._ipps._tcp.local, Ptr _universal._sub._ipp._tcp.local
[21:32:08.6Z] if=13  Ethernet 2     from 192.168.1.154     115B  QUERY  Ptr _universal._sub._ipp._tcp.local, Ptr _universal._sub._ipps._tcp.local
[21:32:09.6Z] if=13  Ethernet 2     from 192.168.1.154     115B  QUERY  Ptr _universal._sub._ipp._tcp.local, Ptr _universal._sub._ipps._tcp.local
[21:32:12.6Z] if=13  Ethernet 2     from 192.168.1.154     135B  QUERY  Ptr _universal._sub._ipp._tcp.local, Ptr _universal._sub._ipps._tcp.local, Aaaa secretp...
[21:32:24.2Z] if=13  Ethernet 2     from 192.168.1.154     115B  QUERY  Ptr _universal._sub._ipp._tcp.local, Ptr _universal._sub._ipps._tcp.local
[21:32:25.2Z] if=13  Ethernet 2     from 192.168.1.154     115B  QUERY  Ptr _universal._sub._ipp._tcp.local, Ptr _universal._sub._ipps._tcp.local
```

`Listen` shortens a long list of names to its first 87 characters and adds
`...`. In the 21:30:40.2 line the kept text ends `_ipp.`, so the name that was
cut is `_universal._sub._ipp._tcp.local`, not the `_ipps` one.

The same two devices asked many more times for records the advertisement does
not hold: AAAA and type 65 for `secretprinter.local`, and the PTR for
`_universal._sub._ipps._tcp.local`. The responder has nothing to answer those
from. An NSEC stating that `secretprinter.local` has no AAAA is the subject of
`REQ-ADV-022`, which is not yet implemented.

No packet from `192.168.1.161` answered any of the 15. The only packets from
that address in the 30 minutes were four identical query-and-reply pairs at
21:35:20Z and 21:35:21Z (dev-box clock) about `FIOS-STB-01.local`, TTL 60.
That is the machine's own name, which SecretPrinter does not publish.

## Run 2: the goodbye and the announcements

The service withdrew at 22:03:07Z and restored at 23:12:25Z (see "Also
recorded" below). With it advertising again, `Listen` was started on the dev
box at 23:22:28Z with `--duration 300` and stopped by hand once the
announcements had arrived. Edwin then ran `Restart-Service SecretPrinter` on
FIOS-STB-01.

On the dev box:

```
[23:22:56.5Z] if=13  Ethernet 2     from 192.168.1.161     883B  REPLY  ttl=[0, 0, 0, 0, 0, 0]  Ptr _ipp._tcp.local, Ptr _universal._sub._ipp._tcp.local, Ptr _services._dns-sd._udp.lo...
[23:22:58.2Z] if=13  Ethernet 2     from 192.168.1.161     883B  REPLY  ttl=[4500, 4500, 4500, 120, 4500, 120]  Ptr _ipp._tcp.local, Ptr _universal._sub._ipp._tcp.local, Ptr _services._dns-sd._udp.lo...
[23:22:59.2Z] if=13  Ethernet 2     from 192.168.1.161     883B  REPLY  ttl=[4500, 4500, 4500, 120, 4500, 120]  Ptr _ipp._tcp.local, Ptr _universal._sub._ipp._tcp.local, Ptr _services._dns-sd._udp.lo...
[23:23:00.2Z] if=13  Ethernet 2     from 192.168.1.161     883B  REPLY  ttl=[4500, 4500, 4500, 120, 4500, 120]  Ptr _ipp._tcp.local, Ptr _universal._sub._ipp._tcp.local, Ptr _services._dns-sd._udp.lo...
```

In the service log on FIOS-STB-01:

```
2026-09-23 23:22:57Z  INFORMATION  Service 'SecretPrinter' stopping.
2026-09-23 23:22:57Z  INFORMATION  No longer accepting print jobs on Ethernet.
2026-09-23 23:22:57Z  INFORMATION  Advertisement retracted (goodbye records sent).
...
2026-09-23 23:22:58Z  INFORMATION  SecretPrinter starting.
...
2026-09-23 23:23:01Z  INFORMATION  Announced. Answering queries.
```

The goodbye carries six answers, every one with TTL 0. The first three names
`Listen` printed are the three PTRs; the rest were cut short. The announcements
are the same size and carry the same six, with the TTLs the service logged for
its advertisement at 23:22:59Z, record by record: the three PTRs at 4500, the
SRV at 120, the TXT at 4500 and the A at 120.

The clock note in the setup comes from here. `OnStop` in `WindowsService.cs`
writes the `stopping` line before it cancels the work that sends the goodbye.
The log truncates to the second, so that line was written at 23:22:57.0Z or
later by FIOS-STB-01's clock, and the goodbye left after it. The dev box
received it at 23:22:56.5Z by its own.

**What this establishes.** On shutdown, the goodbye leaves FIOS-STB-01 on the
client network and reaches another host there, with every record at TTL 0, as
`REQ-LIF-003` requires. The
[2026-09-04 finding](2026-09-04-windows-service-run.md) listed this under
"Still not directly observed"; for IPv4 it now has been. The three announcements, one
second apart, were seen the same way.

**What it does not.** Whether any client applied the goodbye: no phone was
watched during this run. Anything over IPv6: announcements and goodbyes go out
over IPv4 only, by a deliberate choice commented in `MdnsResponder`, and
`Listen` does not listen on IPv6 in any case. The goodbye sent at the
22:03:07Z withdrawal (`REQ-LIF-006`) was not observed, because `Listen` was not
running then.

## The service's own count

At the stop, 23:22:57Z:

```
Served 21 quer(ies) of 481 seen; 460 were for other services and were ignored.
By transport: IPv4 answered 0 of 37 seen; IPv6 answered 21 of 444 seen.
```

One `MdnsResponder` serves a run of the service, so these counts cover this
run from its 20:35:07Z start to the stop, and all of Run 1. They leave out the
withdrawn period, 22:03:07Z to 23:12:25Z, because `HandleAsync` returns before
counting while the advertisement is withdrawn.

The two lines add up exactly: 37 + 444 = 481 seen, and 21 answered + 460
ignored = 481. In `HandleAsync`, a query once counted as seen is answered, or
ignored because nothing in the advertisement matches it, or reaches a send
that throws before it can be counted as answered. The totals leave no room for
the third. So every one of the 37 IPv4 queries the responder saw asked for
nothing it advertises, and none was answered by any route: an answer sent by
unicast to a legacy querier is counted as answered too.

## What that leaves open

Fifteen IPv4 queries asking for advertised names were on the client network
during Run 1, and not one of the IPv4 queries the responder counted asked for
an advertised name. So those 15 either never reached the responder's IPv4
socket, or reached it and were not matched. This run cannot say which: the
counters do not record which queries they saw, and `Listen` ran on another
machine.

The devices connected to the proxy all the same. The service answered 21
queries over IPv6 in this run; which devices those came from, and when, the
counters do not say. How each device found the proxy in these minutes is
therefore not established, though IPv6 is the only transport the service
answered on.

This bears on `REQ-ADV-018`, which has the service answer over IPv6 "in
addition to IPv4". Its IPv4 half was not seen working here. The only other
per-transport count on record, in
[2026-09-14](2026-09-14-ipv6-is-the-only-transport.md), also shows no IPv4
answer, with no IPv4 query seen.

It also bears on what earlier findings said about iOS. Captures taken on
FIOS-STB-01 on 2026-09-06 ([finding](2026-09-06-ipv6-mdns-transport.md))
showed no IPv4 mDNS from the phone, and on 2026-09-14 no IPv4 query reached the
service. From the dev box on 2026-09-23, IPv4 queries from `.152` and `.154`
were on the network. Whether iOS behaved differently on those days, or IPv4
queries from these devices reach the dev box and not FIOS-STB-01, is not
established.

**What would tell these apart:** `Listen` run on FIOS-STB-01 itself, joined on
`Ethernet` alongside the service, while a device looks for the printer. If the
IPv4 queries arrive there, they reach the machine and the responder is not
matching them. If they do not, they are not reaching the machine at all.

## Corrections made with this finding

- [2026-09-14](2026-09-14-ipv6-is-the-only-transport.md). Its title, one
  passage and its closing sentence said or endorsed that iOS asks only over
  IPv6, and its status called `REQ-ADV-018` validated when only its IPv6 half
  was. What it measured, that no IPv4 query reached the service in 87 seconds,
  stands. The text is corrected, with the original words kept in a dated note
  at the top. Its file name still carries the original claim; it is kept
  because other documents link to it.
- [2026-09-21, the withdrawal](2026-09-21-the-withdrawal-on-hardware.md). Its
  first gap said iOS asks only over IPv6. Corrected the same way.

## The same claim, not corrected here

These still say, or lean on, iOS asking over IPv6 alone. None is changed by
this finding:

- `docs/findings/2026-09-06-ipv6-mdns-transport.md`, under "What this implies
  for the specification": "A client network offering IPv6 makes iOS query over
  IPv6 exclusively". Its capture results are measurements of that day and
  stand.
- `docs/findings/2026-09-21-a-second-printer-entry-on-the-iphone.md`, under
  "What this does not establish": "The iPhone queries exclusively over IPv6".
- `docs/operating.md`, "Uninstalling": "The goodbyes go out over IPv4 only, and
  iPhones ask over IPv6." True as far as it goes, but the warning that follows
  depends on reading it as "only".
- `src/SecretPrinter.Service/ServiceHost.cs`, a comment: "iOS was measured
  querying over IPv6 alone".

A related remark, about names rather than transports: a comment in
`src/SecretPrinter.Advertising/AdvertisementBuilder.cs` says the AirPrint
subtype was "measured to be the only name iOS queries for". In Run 1 both
devices also asked for `_universal._sub._ipps._tcp.local`. Not examined further.

## Also recorded, not chased

- **The printer side dropped.** At 22:03:07Z the service logged the printer
  unreachable and withdrew. Afterwards the Broadcom `Wi-Fi` showed
  `Disconnected`, and Edwin reconnected it with `netsh wlan connect` at a time
  not recorded. The service logged the printer reachable again at 23:12:23Z and
  restored at 23:12:25Z, 69 minutes after the withdrawal. `docs/operating.md`
  says the printer "may not reappear for up to an hour after the link returns".
  Whether this stayed within that depends on when the link returned, which this
  run does not have, so it does not measure how long the service took to
  notice. The WLAN-AutoConfig log would date both the drop and the reconnect;
  it was not read.
- **The withdrawal line says nothing about the adapter.** It says the printer
  stopped answering and claims nothing about why. Whether the adapter was
  already down at 22:03:07Z is not known, so whether the line should have said
  so is not established.

## Prediction record

Claude predicted:

- After Run 1 and before the service log was read, that the log would show a
  withdrawal before 21:29:18Z, which would explain why nothing answered the
  IPv4 queries. **Did not hold.** The service was advertising throughout.
- For Run 2, one reply from `192.168.1.161` with all six TTLs 0 at the stop.
  **Held.**
- Then three replies from `192.168.1.161`, each with
  `ttl=[4500, 4500, 4500, 120, 4500, 120]`. **Held.**
- `IPv4 answered 0 of 0 seen` in the shutdown summary. **Did not hold:** 0 of
  37 seen. Claude had said that IPv4 queries seen and some answered would mean
  answers lost on the way. What came was neither: queries seen, none for an
  advertised name, none answered.
