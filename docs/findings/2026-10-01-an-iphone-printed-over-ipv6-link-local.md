# An iPhone printed over IPv6 link-local

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-01. Reviewed by a human before merge.*

**Status: `REQ-ADV-021` verified on hardware. FIOS-STB-01, running `2c93864`,
published an `AAAA` record for its client interface's link-local address and
listened on it, and an iPhone printed two pages through it, over IPv6, on
2026-10-01. README open question 11 is answered for this iPhone. Three things
seen in the same capture are not explained yet and are listed at the end.**

*(Corrected 2026-10-01 by Claude, Claude Opus 5.5: the second of those first
said that whether IPv4 connections end with the same resets was not
established. The 2026-09-29 finding had already recorded it on IPv4
connections; Claude had not read it before writing that line.)*

No IPv6 address, MAC address or device name appears in this finding. They are
household values. The capture holds them, and the printed pages too, so it is
kept outside the repository.

## The test bed, as measured today

- **FIOS-STB-01**, Windows 10 22H2, running the service as `LocalService`,
  updated to `2c93864` by the procedure in [`operating.md`](../operating.md):
  stop, pull, publish, start. `SecretPrinter.Service.dll` was written at
  14:30:01Z and the service logged its start at 14:30:24Z.
- **Client side:** `Ethernet`, the Broadcom NetLink, 192.168.1.161, IPv6 index
  15.
- **Printer side, changed since 2026-09-29:** the configuration names `Wi-Fi`,
  which is now a TP-Link AC600 USB adapter, at 192.168.12.163 with interface
  index 32. No adapter named `Wi-Fi 3`, the Linksys used until then, is listed.
  `BNA`, the built-in Broadcom wireless, shows as Not Present; it was recorded
  on 2026-09-29 as disabled, and what changed it is not established.
- **The printer** at 192.168.12.180, reached over TLS 1.2 with the pinned
  certificate on every job, as the log shows.

## What the service logged at startup

One link-local address was published for `Ethernet`, scoped to 15, as an
`AAAA` record with a 120-second lifetime. When the printer was offered, two
listeners opened: on 192.168.1.161:631, permitting 192.168.1.0/24, and on the
link-local address, port 631, permitting fe80::/10 on scope 15. Binding a
listener to a link-local address with its scope worked on this machine.

## The capture

pktmon on the Broadcom NetLink only, TCP 631 and UDP 5353, whole packets, from
14:36:53 to 14:42:16 UTC: 2002 packets, none dropped. SHA-256
`C787B7CFB78E875C7A4609A32A391F41D563922B6AB9FB243329FF8EFD5C7271`, the same on
FIOS-STB-01, in its OneDrive copy, and as uploaded for reading.

The Broadcom's pktmon component Id was **12**. The handoff's capture command
named Id 10, read from `pktmon list` on 2026-09-29, before the TP-Link was
added. Run as written, it would have captured another component. The command
used today started only if `pktmon list` showed Id 12 beside the Broadcom's MAC
address.

## What happened

- **The client.** Every packet from the client's link-local address came from
  one network card, which also sent four IPv4 packets. Its mDNS questions are
  an Apple device's (`_companion-link`, `_rdlink`, AirPlay, the IPP printer
  subtypes). Edwin printed from his iPhone; the capture does not name the
  device, so that it is the iPhone is an inference from the timing and from
  what it asked.
- **mDNS.** FIOS-STB-01 sent 24 responses, all over IPv6 to `ff02::fb`, and
  every one carried both the link-local `AAAA` record and the `A` record. No
  device asked about the host name directly in this window; the iPhone
  received the `AAAA` as an additional record behind the service answers.
- **Connections.** 42 attempts to port 631. 41 went to the link-local address.
  One went to 192.168.1.161 at 14:39:37: the iPhone sent SYN, FIOS-STB-01
  answered SYN-ACK within 1 ms, and the iPhone reset it 6 ms later, in the
  second the log records an IPv6 connection accepted from it. That fits a
  client racing both families and keeping the faster (RFC 8305); how iOS
  decides was not measured.
- **Printing.** 215 IPP requests and 203 decoded replies, every reply
  `successful-ok`. Two jobs: Validate-Job, Create-Job and Send-Document at
  14:39:59 to 14:40:04, and again at 14:41:04 to 14:41:09, the two pages Edwin
  saw print. Every job line in the service log names the link-local client on
  scope 15, and every relay to the printer used TLS 1.2 with the certificate
  matching the pin.

## The first attempt

Edwin's first attempt, from 14:37:49, did not print, and he thought it timed
out. In that window every request got a `successful-ok` reply, the printer
reported no problem (`printer-state-reasons` was `none` in all 174 replies that
carried it) and no Create-Job was sent. On the first connection the capture
shows only 195 of the 2,644 reply bytes the log reports, but the iPhone
acknowledged all 2,644 within 1.1 seconds. The rest was delivered and missed by
the capture. The iPhone then held that connection idle for 17 seconds and
closed it.

So the capture shows the relay answering everything in the first attempt.
What the iPhone showed, and why it did not go on to print, is not established.

*(Status note, 2026-10-02, by Claude, Claude Opus 5.5: the same thing happened
twice more on 2026-10-02, with the same byte counts on the first connection,
and the iPhone showed a message that it could not reach the printer. It is
still not explained. See
[the finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md).)*

## Open question 11

*Offered both an `A` record and a link-local `AAAA` record, which address does
iOS connect to, and does it connect to a link-local address it learned over
mDNS at all?* For this iPhone: the link-local address, for every connection
that carried data, and printing works over it. Other iPhones, other iOS
versions and other Apple devices were not measured, and this iPhone's iOS
version was not recorded.

## Also seen, not explained

1. **Direct unicast queries went unanswered.** At 14:37:49 the iPhone sent
   SRV and TXT queries for the proxy's instance by unicast to port 5353, both
   to the link-local address and to 192.168.1.161. No answer appears. About
   260 ms later it asked by multicast and was answered within 5 ms, so nothing
   failed. RFC 6762 §5.5 says a responder should answer a direct unicast query.
   Why this one did not is not established; one possibility to test is that the
   Windows DNS Client, which shares port 5353, receives unicast datagrams sent
   to it.
2. **FIOS-STB-01 sent 42 of the 43 resets on port 631**, after the iPhone had
   closed its side. This is not new with IPv6: the relay answered a client's
   FIN with a reset on IPv4 connections on 2026-09-29
   ([finding](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md)).
   Why is not established. No job was affected.
3. **The capture has gaps.** tshark marks 10 segments as missing, although
   pktmon reported none dropped, and the iPhone's acknowledgements show the
   data arrived. Reading a pktmon capture, an acknowledgement is better
   evidence of delivery than the absence of a packet.

   *(Status note, 2026-10-02, by Claude, Claude Opus 5.5: in the captures of
   2026-10-02 the gaps are replies larger than one packet, recorded before the
   adapter splits them, which tshark does not read as TCP. This capture was not
   read again to confirm the same of it. See
   [the finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md).)*

## Prediction record

Claude predicted:

- For the update: the pull ending at `2c93864`, the publish succeeding, the
  service running, the DLL written seconds before the start line, one
  link-local address published and two listeners opened. **Held.** The scope,
  which Claude did not predict, was 15.
- For the printer side, after Edwin said he had a new adapter: either the
  configuration names `Wi-Fi` and the TP-Link is in use, or it still names
  `Wi-Fi 3` with the Linksys present. **The first held.** Also predicted:
  `BNA` disabled, as recorded. **Did not hold:** it is Not Present.
- For `pktmon list`: both adapters listed, the Broadcom's Id possibly changed.
  **Held; it was 12.**
- Which address iOS would connect to was not predicted; that was the question.
