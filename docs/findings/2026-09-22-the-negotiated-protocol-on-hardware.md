# The negotiated protocol, logged against the real printer

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01. With `4e9e8b7` installed, the iPhone opened twelve
relayed connections in the course of one print, and every one logged
`encryption to printer: TLS 1.2, certificate matched the pinned fingerprint`.
All twelve completed; none failed. The page printed. This is the part of
`REQ-OBS-008` the unit tests could not show: the protocol read from a real
handshake with the printer, not from a value a test supplied.**

## Why this was run

`4e9e8b7` added the per-connection half of `REQ-OBS-008`: each relayed
connection's log line names the TLS protocol its handshake negotiated. The unit
tests cover every step but the first. They cannot complete a TLS handshake (see
the header of `tests/SecretPrinter.Proxy.Tests/TlsConnectionFactoryTests.cs`),
so no test shows `TlsConnection` reading `SslStream.SslProtocol` after a real
one. That is one assignment in its constructor, and only the printer can show
it working.

## What was installed

FIOS-STB-01, Windows 10 build 19045, the service registered as `SecretPrinter`
under `NT AUTHORITY\LocalService`. Edwin pulled `4e9e8b7`, published to
`C:\Program Files\SecretPrinter`, and started the service. All commands were run
by Edwin; the outputs quoted are his pastes.

- `SecretPrinter.Proxy.dll` written 2026-09-22 16:45:27Z (printed as 12:45:27 PM
  local, UTC-4); the service's start line at 16:48:42Z. The file was written
  three minutes before the start, by the publish that preceded it.
- Configuration in force, from the startup log: client side `Ethernet` ->
  `192.168.1.161` (index 15); printer side `Wi-Fi 2` -> `192.168.12.136`
  (index 49), the Compact Wireless-G USB adapter; the certificate pin as
  configured.
- The machine's other Wi-Fi adapter, the Broadcom (`Wi-Fi`, index 9), had been
  disabled by Edwin with `Disable-NetAdapter` shortly before, to reduce radio
  emissions in the room. The service does not use it.

## The print

Edwin printed one page from the iPhone to `SecretPrinter (ET-3760)`. He reports
that it printed correctly and that the printer's supply levels appeared on the
iPhone straight away. The iPhone connected from `192.168.1.156`; the previous
handoff recorded it at `192.168.1.152`. Its address had changed; why was not
looked at.

## What the log recorded

Every `Job:` line from 16:50Z to 16:59Z, from
`C:\ProgramData\SecretPrinter\secretprinter.log`. Every connection was located
from the cache (`cached, 0s`) at `192.168.12.180:631`, and every `relaying` line
ended `encryption to printer: TLS 1.2, certificate matched the pinned
fingerprint.`

| Client port | Relaying from | Bytes to printer | Bytes to client | Duration | Ended |
|---|---|---:|---:|---:|---|
| 56766 | 16:50:07Z | 18791 | 18988 | 39 s | completed |
| 56767 | 16:50:09Z | 5387 | 5632 | 37.41 s | completed |
| 56768 | 16:50:09Z | 8146 | 9272 | 37.41 s | completed |
| 56769 | 16:50:09Z | 2628 | 1992 | 37.41 s | completed |
| 56780 | 16:50:38Z | 53278 | 3279 | 7.96 s | completed |
| 56781 | 16:50:43Z | 600 | 297 | 3.38 s | completed |
| 56784 | 16:50:47Z | 55321 | 43580 | 149.03 s | completed |
| 56785 | 16:50:48Z | 5064 | 2541 | 48.39 s | completed |
| 56786 | 16:50:48Z | 687 | 353 | 30.97 s | completed |
| 56788 | 16:51:44Z | 12614 | 11110 | 90 s | completed |
| 56789 | 16:51:44Z | 657 | 498 | 30.47 s | completed |
| 56790 | 16:51:44Z | 1445 | 2644 | 30.43 s | completed |

Twelve accepted, twelve relayed, twelve completed, none failed or refused. The
last completed at 16:53:15Z.

Each line is a *connection*, which the log calls a "Job". All twelve came from
the iPhone between 16:50:06Z and 16:51:44Z, while Edwin chose the printer and
printed one page. Which of them belonged to choosing the printer, reading its
supplies or sending the page is not known.

## What this establishes

- On this machine, against this printer, every relayed connection negotiated
  TLS 1.2, and the service logged that on the line recording the connection,
  as `REQ-OBS-008` requires. The whole chain ran: the handshake, the protocol
  read from it, its name, the relay passing it on, and the service log writing
  it.
- The value came from real handshakes. No test supplied it.
- Together with the startup line that records the pinned fingerprint, an
  operator can read from the log alone that each connection was encrypted, with
  what, and that the device answering held the pinned certificate.

## What this does not establish

- **Which connection carried the page.** The log holds endpoints, counts and
  durations, never content, by design (`REQ-OBS-004`). Two connections carried
  over 50 KB to the printer (56780, 53278 bytes; 56784, 55321 bytes). Either,
  both or neither could be the document. Nothing here can say which, and the
  service is built so that it cannot know.
- **That "certificate matched the pinned fingerprint" was checked twelve
  separate times from this log.** That half of the line is true of every
  `TlsConnection` by construction: one is only made after the pin approves the
  certificate. The line reports that fact; it is not an extra observation per
  connection. The pin's refusal of a wrong certificate on this hardware is
  recorded in `2026-09-18-the-pin-refuses-a-mismatch.md`.
- **Why the protocol was TLS 1.2 and not TLS 1.3.** Microsoft's Schannel
  documentation, cited in `TlsConnectionFactory.cs`, says TLS 1.3 is supported
  from Windows 11 and Windows Server 2022. FIOS-STB-01 runs Windows 10, so 1.2
  is what that documentation leads one to expect. Which side limited it was not
  examined, and the `TLS 1.3` branch of the log text has not run on hardware.
- **Why the iPhone opened twelve connections**, or what each carried. That is the
  iPhone's behaviour and the printer's, and it was not looked at.
- **Anything about another machine or another printer.**

## Prediction record

Before the print, Claude predicted:

- The `relaying` line would read `TLS 1.2, certificate matched the pinned
  fingerprint`, reasoning from the Windows 10 documentation above. **Held.**
- Several groups of `Job:` lines, since iOS usually opens more than one
  connection per print. **Held:** twelve connections.
- Before the full log was read, that 56784, 56785 and 56786 would each end in
  `completed`, and that one connection would carry tens of kilobytes. **Held**,
  with two over 50 KB rather than one.
- That the iPhone would connect from `192.168.1.152`, the handoff's address.
  **Wrong:** it was `192.168.1.156`. Claude did not write "iPhone" into the
  record until Edwin confirmed which device printed.

Claude also said, from the first partial look at the log, that 56786's 687
bytes were "the size of a status query rather than a page", and connected it to
the supply levels appearing. That was an inference from a size, and nothing
here confirms what 56786 carried.
