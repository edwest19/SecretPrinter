# With everything up, the iPhone dropped the printer within seconds of the goodbye

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-25. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01, the dev box and the iPhone, 2026-09-25 UTC
(the evening of 2026-09-24, local time). With the printer side up and the
service advertising, the service was stopped at 03:17:10Z. Its goodbye reached
the dev box, which is behind the same access point as the iPhone, at 03:17:10.3Z
with every record at TTL 0. The iPhone's printer list no longer showed
`SecretPrinter (ET-3760)` about three seconds after the stop finished. This
measures the time the
[2026-09-21 withdrawal finding](2026-09-21-the-withdrawal-on-hardware.md) left
unmeasured, for a stop. It does not explain why the entry stayed after that
finding's withdrawal. Nothing in the service was changed.**

## The question

The 2026-09-21 withdrawal finding recorded, as its first gap, that the iPhone
kept listing the printer after the goodbye the withdrawal sent, and that how
long it stayed was not measured.

The service sends the same goodbye whether it stops or withdraws: both paths in
`ServiceHost` call `MdnsResponder.SendGoodbyeAsync`, which sends every
advertised record at TTL 0, over IPv4 only. This run triggered it with a stop,
so that it went out at a known moment. A withdrawal fires only when the
printer's records expire, some time after the printer goes quiet.

## The setup

The configuration in force, from the service's startup lines: client interface
`Ethernet` → `192.168.1.161` (index 17), printer adapter `Wi-Fi` →
`192.168.12.186` (index 10). The network is as described in the
[2026-09-24 finding](2026-09-24-ipv4-mdns-from-behind-the-access-point.md).

Edwin asked that the run start with everything known to be up. Just before it,
on FIOS-STB-01 (`netsh wlan show interfaces` filtered to its `State` line,
`Test-NetConnection 192.168.12.180 -Port 631 -InformationLevel Quiet`, and the
last three lines of the service log):

```
    State                  : connected
True
2026-09-25 02:58:53Z  INFORMATION  Printer reachable again: it answered on the printer-side interface.
2026-09-25 02:58:53Z  INFORMATION  Accepting print jobs on 192.168.1.161:631 (Ethernet); permitted clients: 192.168.1.0/24.
2026-09-25 02:58:55Z  INFORMATION  Advertisement restored: the printer is on offer again.
```

The Broadcom `Wi-Fi` was connected, the printer answered on port 631, and the
service was advertising.

- **Control:** `SecretPrinter.Listen` ran on the dev box (`Ethernet 2`,
  `192.168.1.234`), which is behind the same access point as the phone, saving
  its output to a file.
- **The phone:** the iPhone (`192.168.1.152`), with Auto-Lock off, had a Print
  sheet's printer list open. At 11:14 pm local time (03:14Z) by the phone's
  clock the list, headed "Known Printers", showed two entries:
  `EPSON ET-3760 Series`, and `SecretPrinter (ET-3760)` with the proxy's note
  under it. Opening the sheet
  made the phone connect through the proxy three times, at 03:14:46Z and
  03:14:47Z by the service log.

## The stop

At FIOS-STB-01's own keyboard, in an elevated PowerShell, Edwin ran
`Stop-Service SecretPrinter` followed by a counter of seconds since
`Stop-Service` returned. `Stop-Service` returns once the service has stopped,
so the goodbye had already gone out when the counter started.

The service log:

```
2026-09-25 03:17:10Z  INFORMATION  Service 'SecretPrinter' stopping.
2026-09-25 03:17:10Z  INFORMATION  No longer accepting print jobs on Ethernet.
2026-09-25 03:17:10Z  INFORMATION  Advertisement retracted (goodbye records sent).
2026-09-25 03:17:10Z  INFORMATION  Served 56 quer(ies) of 1713 seen; 1657 were for other services and were ignored.
2026-09-25 03:17:10Z  INFORMATION  By transport: IPv4 answered 22 of 1321 seen; IPv6 answered 34 of 392 seen.
2026-09-25 03:17:10Z  INFORMATION  SecretPrinter stopped.
```

The counts cover the service run that announced at 01:53:33Z.

On the dev box, this was the only goodbye from `192.168.1.161` in the file:

```
[03:17:10.3Z] if=13  Ethernet 2     from 192.168.1.161     883B  REPLY  ttl=[0, 0, 0, 0, 0, 0]  Ptr _ipp._tcp.local, Ptr _universal._sub._ipp._tcp.local, Ptr _services._dns-sd._udp.lo...
```

The log truncates to the second, and the dev box's clock was found on 2026-09-23
to run at least half a second behind FIOS-STB-01's. Both fit a goodbye that
left during 03:17:10 by FIOS-STB-01's clock.

On the iPhone, Edwin watched the list. `SecretPrinter (ET-3760)` was gone when
the counter read 3 seconds; he described it as immediate. At 11:19 pm local
(03:19Z) by the phone's clock the list showed only `EPSON ET-3760 Series`.

## Afterwards

Edwin ran `Start-Service SecretPrinter`. The service announced at 03:23:35Z. At
03:23:37Z it logged a connection from `192.168.1.152` through the proxy, two
seconds after the announcement.

Edwin reported that `SecretPrinter (ET-3760)` came back on the list. At 11:29 pm
local time (03:29Z) by the phone's clock, the list showed
`SecretPrinter (ET-3760)` first, with the proxy's note, and
`EPSON ET-3760 Series` second, with a check mark beside it. He reported that
when the list opens, SecretPrinter appears first and `EPSON ET-3760 Series`
about a second later, and that both work.

## What this establishes

- With the printer side up and the service advertising, the goodbye a stop
  sends reaches the phone's side of the client network. The iPhone removed the
  entry from its printer list within about three seconds of the service
  stopping.
- For a stop in that state, the time the 2026-09-21 finding left unmeasured is
  seconds, not the 75 minutes that expiry of the 4500-second PTR record would
  take.
- An entry listed under "Known Printers" is removed by a goodbye. That heading
  alone did not keep it.

## What it does not establish

- **Why the entry stayed after the 2026-09-21 withdrawal.** That goodbye goes
  through the same method, but it was not observed on the wire, so whether it
  reached the phone's side is not known.
- **That the phone applies a withdrawal's goodbye.** Only a stop was run.
- **Which transport the phone had learned the records over.** Its IPv6 questions
  are answered over IPv6: 34 were answered in this service run. The
  announcements the service sends at startup and at a restore go out over IPv4,
  and reach the phone's side of the network. Either or both copies may have
  been in its cache. So this run does not settle whether iOS applies an IPv4
  goodbye to records it learned only over IPv6.
- **Other phones, other iOS versions, or a second run.**

## Also recorded, not chased

- **The EPSON entry stayed while the service was stopped.** So it does not come
  from SecretPrinter's advertisement. After the restart it appeared about a
  second after SecretPrinter when the list opened, and Edwin reports that it
  works. What it connects to was not checked. This bears on the open
  [second printer entry](2026-09-21-a-second-printer-entry-on-the-iphone.md).
- **A connection cut by the stop was not reported.** Of the three connections
  the phone opened at 03:14:46–47Z, two logged `completed`, at 03:15:18Z and
  03:15:45Z. The third, from port 52241, has no `completed` line before the
  stop, so it was presumably still open when the service stopped. This matches
  the fourth gap in the 2026-09-21 withdrawal finding, here at a stop rather
  than a withdrawal.
- **The printer side dropped and recovered repeatedly.** After the 06:01:05Z
  start on 2026-09-24, the service withdrew at 06:24:07Z, 06:50:36Z and
  07:54:41Z. The first two restored at 06:42:03Z and 06:51:01Z, with no
  restart in the log.
  - Shortly before 01:53Z on 2026-09-25 the Broadcom `Wi-Fi` was found
    `disconnected`. Edwin reconnected it with `netsh wlan connect`,
    `Test-NetConnection` succeeded over it, and after a restart the service
    announced at 01:53:33Z.
  - It later withdrew again and restored at 02:58:55Z. When that withdrawal
    happened was not examined.
  - Whether each loss was the adapter or the printer is not in the log, by
    design.
- **A `Listen` run lost half an hour of timing.** An earlier run, started
  01:55:15Z, read nothing from 02:23:11.7Z to 02:53:24.5Z. It then read a
  burst of packets from five hosts in 0.3 s, all stamped 02:53:24.5–24.8Z.
  - Among them was a goodbye from `.161` and, straight after it, three
    announcements. Edwin did not stop or restart the service, so they came
    from its own withdrawal and restore. The service log for that window was not
    read.
  - `Listen` stamps a packet when it reads it (`DateTime.UtcNow` after the
    receive returns), not when it arrives. A process that stops reading
    therefore stamps a whole queue of packets with one time, and any packets
    beyond the socket's buffer are lost unseen.
  - Its README calls the stamp "the time the packet arrived", which is wrong
    in that case. Why the process stopped reading is not established. Not
    changed here.
- **`Listen` refuses a `--duration` over 3600 s,** and its README does not say
  so. Not changed here.
- **FIOS-STB-01 announced its own name** at 03:21:00Z, four minutes after the
  stop, as the 2026-09-24 finding also saw. Why is not known.
- **The iPhone asked over IPv4** for `secretprinter.local` and the
  `_universal._sub._ipp` and `_ipps` subtypes at 01:58:25–52Z. Nothing from
  `.161` answered them, as in the 2026-09-24 finding.

## Prediction record

Claude predicted:

- That the Broadcom `Wi-Fi` would be `disconnected` when checked before the
  01:53Z restart. **Held.**
- That `SecretPrinter (ET-3760)` would still be listed three minutes after the
  stop, as on 2026-09-21. **Did not hold:** it was gone within about three
  seconds.
- That `EPSON ET-3760 Series` would not change. **Held.**
- That after the restart `SecretPrinter (ET-3760)` would reappear in an open
  list within a few seconds. **Held,** by Edwin's account; the time was not
  measured.
- That the log would show the stopping, retracted and count lines together
  between 03:14Z and 03:19Z, with nothing after. **Held.**
- That the last `.161` lines in the `Listen` file would be the goodbye. **Did
  not hold:** FIOS-STB-01's own name announcements at 03:21:00Z came after it.
- That the file would hold one goodbye, at about 03:17:09.5–10.0Z. **One
  goodbye held. The time did not:** it was stamped 03:17:10.3Z, which still fits
  the log's second.
