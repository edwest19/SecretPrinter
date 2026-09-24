# The iPhone lists a second printer, and it leads to the proxy

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21. Reviewed by a human before merge.*

*Corrected 2026-09-24 by Claude (Anthropic model, Claude Opus 5.5), at the
direction of Edwin West. Under "What this does not establish", the IPv6 point
said "The iPhone queries exclusively over IPv6", citing the 2026-09-14 finding,
which measured what reached FIOS-STB-01 rather than what the phone sent. On
2026-09-24 the iPhone was seen sending the same mDNS queries over IPv4 and
IPv6, and only the IPv6 copies reached FIOS-STB-01
([2026-09-24](2026-09-24-ipv4-mdns-from-behind-the-access-point.md)). The point now
says that. What it leaves open, an advertisement present only over IPv6, is
unchanged. All the places that carried the claim are listed in
[a separate finding](2026-09-24-the-ipv6-only-claim-was-in-more-places.md).
Reviewed by a human before merge.*

**Status: observed twice in different forms, cause NOT established. The second
entry went away when the iPhone was restarted. Recorded and deliberately left
open; nothing in the code was changed because of it.**

## What was seen

FIOS-STB-01, running `d29dd4e`, service started at 20:17:36Z. The iPhone is on the
client network (it connects from `192.168.1.152`). It is an iPhone; no iPad
has been used in this project's testing.

Between 19:28Z and 19:31Z, before any fault was injected and while the service
was running `1dc116c`, the iPhone's printer list showed
`SecretPrinter (ET-3760)` **once**. That was checked deliberately, as the
baseline for the withdrawal test the same day.

At about 20:18Z the list showed **two** entries: `SecretPrinter (ET-3760)`, and
one Edwin read as "EPSON ET-3760". The exact label was not captured, so it is
recorded here as he reported it. Both entries showed supply levels at once, and
both printed. One page was printed from each, SecretPrinter first.

The service log shows both pages going **through the relay**, from the iPhone, to
the printer:

| Connection | Bytes to printer | Bytes back | Completed |
|---|---|---|---|
| `192.168.1.152:52842` | 39,094 | 5,876 | 20:19:44Z |
| `192.168.1.152:52851` | 38,999 | 5,820 | 20:20:53Z |

No other page-sized connection appears. So the second entry does not reach the
printer directly; it resolves to the proxy at `192.168.1.161`.

An earlier form of this: on 2026-09-17, during the first end-to-end print (on
the dev box, from a phone), two entries named "SecretPrinter" appeared in the
phone's list. The first was used and printed. That was not written down at the
time; it is recorded here so the two observations are in one place.

## What was measured

`tools/SecretPrinter.Probe --interface 192.168.1.161`, run on FIOS-STB-01 after
the two prints, found exactly one instance on the client network:
`SecretPrinter (ET-3760)._ipp._tcp.local`, SRV to `secretprinter.local:631`,
`secretprinter.local -> 192.168.1.161`, TXT `UUID=8d16be42-…` — the proxy's own
UUID. Nothing else answered for `_ipp._tcp` or `_universal._sub._ipp._tcp`.

So, **over IPv4**, nothing on the client network advertises an EPSON instance,
and our responder answers only with our own records (`REQ-SEC-001`).

## After a restart

Later on 2026-09-21 Edwin restarted the iPhone. Afterwards its list showed only
`SecretPrinter (ET-3760)`. Nothing on FIOS-STB-01 was changed in between.

That fits the second entry being state held on the iPhone rather than something
on the network: a restart clears the phone's caches and does not touch ours. It
does not say which state, or how it came to exist.

## What this does not establish

- **IPv6.** The probe queries over IPv4, so an advertisement present only over
  IPv6 would not have appeared to it. The iPhone was seen sending mDNS over IPv6
  as well as IPv4 ([2026-09-24](2026-09-24-ipv4-mdns-from-behind-the-access-point.md)), so
  an advertisement present only over IPv6 is not ruled out as the source of the
  second entry.
- **Where the name comes from.** "EPSON ET-3760" is not the name of anything we
  publish. The words do appear in two places the iPhone can read:
  - our TXT record, which copies `usb_MFG=EPSON`, `usb_MDL=ET-3760 Series` and
    `product=(EPSON ET-3760 Series)` through from the printer, as the allow-list
    permits;
  - the printer's own IPP replies, which the relay passes through unaltered
    (`REQ-PXY-003`). Those carry the printer's own name and its own UUID,
    `cfe92100-…`, while our mDNS advertisement carries `8d16be42-…`.

  One hypothesis is that iOS builds a second entry when the identity it reads
  over IPP does not match the one it read over mDNS. **That is a hypothesis,
  not a finding.** Nothing here tests it.
- **Why the list had one entry before 19:31 and two at 20:18.** Between those times
  the service withdrew and restored its advertisement, and was later stopped and
  restarted on a new build. Either could be involved, or neither.

## Why it matters, and why it was left

Nothing observed here bypasses the proxy or reaches the printer network from the
client network, so no security claim is contradicted by it. It does matter to
anyone using SecretPrinter: two entries for one printer is confusing, and a stale
or duplicate entry is how a user ends up tapping the one that does not work.

Chasing it properly means looking at the client network over IPv6, and reading
what iOS uses to decide that two printers are one. Edwin decided on 2026-09-21
to record it and return to the open work.
