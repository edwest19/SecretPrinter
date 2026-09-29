# The printer's host name did not come through SecretPrinter

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-29. Reviewed by a human before merge.*

**Status: measured on the client network with three packet captures on
FIOS-STB-01 (running `017e5a8`); no code change. In none of the captures did
SecretPrinter give the printer's host name to the client network, in its mDNS
answers or in the relayed IPP. Both devices seen asking for that name had been
on the printer's own network. One README sentence is affected; see the last
section but one.**

In this finding the printer's own host name is written `EPSON000000.local`, the
redacted placeholder used elsewhere in this repository. The real name is a
household value and is not in the repository.

## The question

During the probing run of 2026-09-29
([finding](2026-09-29-probing-on-hardware.md)) the iPhone asked the client
network for `EPSON000000.local` (`HTTPS`, `AAAA` and `A`). SecretPrinter never
advertises that name (`REQ-ADV-004`). Two sources were possible, as the
[note finding](2026-09-29-the-note-published-the-printers-address.md) records:
the iPhone's own memory of the printer, and the IPP replies the relay passes on
unchanged (`REQ-PXY-002`, `REQ-PXY-003`).

## What the iPhone showed

The iPhone had printed to the Epson directly, on the printer's own network,
before; it has been restarted since and was on the client network only. Edwin
sent four screenshots, taken at 13:23-13:24 local time (17:23-17:24 UTC), and
described the behaviour:

- Its Known Printers list holds two entries: "SecretPrinter (ET-3760)", with the
  note `SecretPrinter proxy. Capabilities read from the printer at
  2026-09-29 04:53:47Z`, and "EPSON ET-3760 Series".
- The print dialog opens with "No Printer Selected". Tapping Printer lists
  SecretPrinter. Tapping it turns the selection into "EPSON ET-3760 Series",
  which then loads the colour and two-sided options. Left alone, the dialog
  selects "EPSON ET-3760 Series" by itself in under a minute.
- Printer Info for the selected printer shows Name and Model "EPSON ET-3760
  Series", no Location, a "Show Printer Web Page" link, and ink levels.

The service's log for 17:20-17:28 UTC, while those screens were in use, records
every connection from `192.168.1.156` as located, relayed over TLS 1.2 with the
certificate matching the pinned fingerprint, and completed. So the entry the
iPhone shows as "EPSON ET-3760 Series" reaches the printer through
SecretPrinter.

## How it was measured

The relay's client leg is plain IPP over TCP, as the README discloses, so a
capture on FIOS-STB-01's client-side adapter shows both directions of what
crosses the client network. The printer side, which is TLS, was not captured.
Windows' built-in Packet Monitor was used (`pktmon`, Windows 10 22H2, build
19045), on the client-side adapter only, keeping whole packets:

```
pktmon filter add SecretPrinterIPP -t TCP -p 631
pktmon filter add SecretPrinterMdns -t UDP -p 5353
pktmon start --capture --comp <the client adapter's pktmon Id> --pkt-size 0 --file-name $env:USERPROFILE\secretprinter-capture.etl
pktmon stop
pktmon filter remove
pktmon etl2pcap $env:USERPROFILE\secretprinter-capture.etl --out $env:USERPROFILE\secretprinter-capture.pcapng
pktmon unload
```

The adapter's pktmon Id came from `pktmon list`, checked against the adapter's
MAC address before starting. The first capture had the port 631 filter only.
The second and third were started only when no connection to port 631 was open,
so every connection in them is seen from its first packet. After each capture
the filters were removed and the driver unloaded, and `pktmon status` and
`pktmon filter list` matched their state beforehand: not running, no filters.

| Capture | Filters | UTC | Packets | What was done |
| --- | --- | --- | --- | --- |
| 1 | TCP 631 | 17:40:17 to 17:43:29 | 783 | The iPhone repeated the steps above without printing |
| 2 | TCP 631, UDP 5353 | 18:12:52 to 18:13:56 | 49 | The iPhone's print dialog was opened and selected "EPSON ET-3760 Series" by itself |
| 3 | TCP 631, UDP 5353 | 19:58:10 to 20:07:28 | 1305 | A second device, which had never printed to the Epson, used SecretPrinter and printed a page |

The pcapng files were read by Claude with `tshark` 4.2.2, each after checking
its SHA-256 against the hash taken on FIOS-STB-01. The times are as `tshark`
shows the pcapng's timestamps, read as UTC; they agree with Edwin's clock at
the second capture (the dialog opened at 14:13 local, 18:13 UTC) and were not
checked against the service's log. The files stay off the repository: they
hold household addresses and names, and the third holds a print job. What is
recorded here are counts and field values, with household values redacted.

## What the captures show

### SecretPrinter never gave out the printer's identity

- **Every IPP request named SecretPrinter's host.** The captures hold 154
  exchanges (100, 3 and 51). Every request carried `Host: secretprinter.local`
  and printer-uri `ipp://secretprinter.local.:631/ipp/print`. Every reply was
  `200` and `successful-ok`.
- **Nothing relayed named the printer.** Each connection was reassembled and
  every byte searched, both directions. The printer's host name, its UUID, its
  printer-side address, the text `ET-3760`, and the attribute names
  `printer-name`, `printer-info` and `printer-make-and-model` appear in none of
  them. No device asked for `printer-name`, `printer-info`,
  `printer-make-and-model`, `printer-uuid` or `printer-uri-supported`.
- **No mDNS answer named it.** None of SecretPrinter's answers in the second and
  third captures names `EPSON000000.local`. No device on the client network
  answered for that name.

### The second device asked for the name in its first print-related query

In the third capture the second device's first print-related mDNS query, at
20:05:17, browsed for AirPrint printers and, in the same packet, asked for
`EPSON000000.local` (`HTTPS`, `AAAA` and `A`). The capture had been running
since 19:58:10, and no earlier packet in it names that host. SecretPrinter's
answer to the browse came about a millisecond later; the device's first IPP
connection came 23 seconds later. It asked for the name in 16 packets, and
nothing answered. It had never printed to the Epson, but Edwin confirms it has
been on the printer's own Wi-Fi.

### The iPhone's name for the printer comes from the iPhone

In the second capture the iPhone asked only for `secretprinter.local`
(`HTTPS`, `AAAA` and `A`), was answered with `A 192.168.1.161` by SecretPrinter,
connected, and asked for capabilities and status. It selected "EPSON ET-3760
Series" in that minute with nothing on the wire naming the printer except the
`Server` header (below) and the model strings SecretPrinter itself publishes in
TXT (`usb_MFG`, `usb_MDL`, `product`), which the iPhone already held and did not
ask for in the capture.

The second device, by Edwin's account (no screenshots could be taken from it),
never selected a printer by itself, listed only SecretPrinter, and kept the
name "SecretPrinter (ET-3760)".

## What this establishes

- SecretPrinter did not carry the printer's host name to the client network in
  any of the three captures: not in its mDNS answers, and not in the relayed
  IPP. Nothing seen supports the IPP replies as the source.
- Both devices seen asking for `EPSON000000.local` had been on the printer's own
  network, and one of them never printed there. Everything seen fits their
  having kept the name from that network; nothing seen fits another source.
- The "EPSON ET-3760 Series" label comes from the iPhone's memory of the
  printer. On a device that had not printed to the printer, the proxy kept its
  own name, which is what `REQ-ADV-016` asks for.
- `REQ-ADV-004` held on the wire for everything captured.

## Also observed

- **The printer builds links from the host the client asked for.** Its replies
  carry `printer-more-info`, `printer-strings-uri` and
  `printer-supply-info-uri`, all `http://secretprinter.local.:80/...`.
  SecretPrinter serves nothing on port 80, so from the client network these
  links lead nowhere. The relay passes them unchanged, as `REQ-PXY-003`
  requires. SecretPrinter publishes no `adminurl` (`REQ-ADV-008`), yet the
  iPhone offers "Show Printer Web Page"; which attribute it uses was not
  established.
- **Every reply carries `Server: Epson_IPP-Server/2.0.0`,** passed unchanged.
  It names the printer's maker to the client network, as the published TXT
  already does.
- **The client leg is plain HTTP/1.1,** with no TLS and no upgrade, as the
  README discloses.
- **The relay answers a client's FIN with a reset.** In the second and third
  captures, on each of the four connections the client closed with a FIN, the
  relay answered with an RST rather than its own FIN; on one of them (20:06:11)
  the relay had sent a FIN first. Nothing failed.
- **The second device's selection fell back once.** By Edwin's account it went
  back to "No Printer Selected" about 30 seconds after its options loaded;
  selecting SecretPrinter again worked, and it printed.
- **`HTTPS` and `AAAA` questions for `secretprinter.local` went unanswered,**
  from both devices. SecretPrinter publishes neither and sends no `NSEC`: the
  subjects of `REQ-ADV-021` and `REQ-ADV-022`, the next items of work.
- **The iPhone's address.** It appears as `192.168.1.156` in the log at
  17:20-17:28, matched to its screenshots by time, and as `192.168.1.152` in
  the first two captures.

## What this means for the README

Section 1 says: "Fixing only the first produces a printer that appears in the
list and then fails on every job. This was observed directly: a tablet on the
client network was captured querying for the printer's `.local` hostname and
receiving no answer."

The first sentence is reasoning and stands. The second does not show it. Devices
that have been on the printer's own network ask for its host name, and get no
answer, even while SecretPrinter works and they print through it, as both
devices here did. A query for the printer's host name that goes unanswered is
not evidence that a printer appeared in the list and then failed. The sentence
is to be corrected in a separate commit; the wording is Edwin's call, and a
status note will be added here when it is done.

*(Status updated 2026-09-29 by Claude, Claude Opus 5.5: done. At Edwin's
choice, Section 1 now reads: "Fixing only the first produces a printer that
appears in the list and then fails on every job: the client is given the
printer's own address, which it has no route to (Section 2)." It rests on the
reasoning Section 2 gives and cites no observation.)*

Whether the links to port 80 above belong among the README's stated limitations
is also Edwin's call.

## Not established

- **What iOS keeps from a network it has left,** for how long, and what it
  matches a printer on. None of it is visible on the wire.
- **A device that has never been on the printer's network** was not tested.
- **Why the relay answers a FIN with a reset,** and whether it matters.
- **Why the second device's selection fell back.**
- **Which attribute "Show Printer Web Page" uses,** and where it leads.
- **Why the iPhone's address changed** between 17:28 and 17:40.
- **Whether the second device had used SecretPrinter before** this test. What a
  device asks on its first connection to a printer is covered only as far as
  the second and third captures go; the first began with connections already
  open.
