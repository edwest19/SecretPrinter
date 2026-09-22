# The withdrawn start, on hardware

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01. With `523bacd` installed and the
printer-side adapter disconnected, the service started, reported itself
running to Windows, logged one warning and offered nothing. When the adapter
was reconnected it came up by itself 8 seconds later, and an iPhone then
printed a page through it. This is the hardware half of `REQ-LIF-008`; the unit
tests in `tests/SecretPrinter.Service.Tests/StartupWaitTests.cs` are the other
half.**

## Why this was run

`REQ-LIF-008` replaced a refusal to start with a wait. The tests show each
wait's logic against a fake adapter and a fake printer. They cannot show that
`ServiceHost.RunAsync` really opens nothing on the client network while it
waits, that Windows accepts a service that is running but not yet offering
anything, or that a real WLAN adapter coming back is seen as usable.

## What was installed

FIOS-STB-01, Windows 10 build 19045, the service registered as `SecretPrinter`
under `NT AUTHORITY\LocalService`. Edwin stopped the service, pulled
`523bacd` and published it; `SecretPrinter.Service.dll` was written 21:38:13Z
and `SecretPrinter.Configuration.dll` 21:38:12Z. All commands were run by Edwin
and the outputs quoted are his pastes. Times are UTC.

## What happened

| Time | Event |
|---|---|
| before 21:39 | `netsh wlan disconnect interface="Wi-Fi 2"`; `Get-NetAdapter` then reported `Status : Disconnected`. |
| 21:39:47 | Service started. The log shows the configuration lines, including the new `printer adapter  : Wi-Fi 2 (resolved when it is usable; logged below)`, then one warning: `Waiting for the printer-side interface: Interface 'Wi-Fi 2' holds 192.168.12.136 but is not up. Nothing is offered to the client network until it is usable and the printer answers.` Nothing followed it: no socket, no query, no announcement. |
| after 21:39:54 | `sc.exe query SecretPrinter`: `STATE : 4 RUNNING`, `WIN32_EXIT_CODE : 0`. The same start at 21:00:12, before this change, failed with exit code 1064. |
| 21:41:34 | Edwin reconnected: `netsh wlan connect name="TMOBILE-9992" interface="Wi-Fi 2"`. |
| 21:41:39 | `The printer-side interface 'Wi-Fi 2' is usable. No longer waiting for it.` and `printer interface: Wi-Fi 2 -> 192.168.12.136 (index 49)`. |
| 21:41:42 | `Announced. Answering queries.` and `Accepting print jobs on 192.168.1.161:631 (Ethernet)`. |
| 21:43:56 onward | Connections from the iPhone. |

From reconnect to accepting jobs took 8 seconds: 5 for the adapter check, which
runs every 5 seconds, and 3 for opening the resolver socket, asking the printer,
opening the responder socket and announcing. The printer answered the first
query, so the log has no `did not answer at startup` line. The address did not
pass through a tentative state that the service saw, so there was only one
`Waiting for` line.

## The print

Edwin printed one page from the iPhone at `192.168.1.156`, and it printed. The
service log for that minute:

- `21:45:08` `Job: accepted from 192.168.1.156:63685`, located the printer's
  `_ipps._tcp` instance at `192.168.12.180:631` (cached), and relayed it with
  `encryption to printer: TLS 1.2, certificate matched the pinned fingerprint`.
- `21:45:12` the same for port 63686.
- Between `21:45:38` and `21:46:04`, six connections completed: 63682, 63683,
  63673, 63671, 63686 and 63685. The largest, 63685, sent 57844 bytes to the
  printer, of the same order as the two largest connections of the
  afternoon's print (53278 and 55321 bytes,
  [the negotiated-protocol finding](2026-09-22-the-negotiated-protocol-on-hardware.md)).
  The earliest to start, 63671, began about 21:43:56, from its completion time
  less its logged duration.

None failed. Which connection carried the page is not recorded, by design: the
log does not tie a connection to a job.

## Seen on the iPhone, not explained

Edwin's screenshots show, under the print dialogue's "Known Printers", two
entries: `SecretPrinter (ET-3760)`, with the proxy's TXT note naming the mDNS
query its capabilities came from, and `EPSON ET-3760 Series`. The printer
information screen and the print queue both named the printer
`EPSON ET-3760 Series`. The proxy did relay the connections above during the
print, so the page went through SecretPrinter, but these observations are not
explained here:

- **Which entry was chosen.** Not recorded. iOS may show the name the printer
  reports for itself over IPP, which the proxy relays unchanged; that is not
  established.
- **Where the `EPSON ET-3760 Series` entry comes from.** "Known Printers" may
  list printers used before rather than printers discovered now. This belongs
  with
  [`2026-09-21-a-second-printer-entry-on-the-iphone.md`](2026-09-21-a-second-printer-entry-on-the-iphone.md),
  which stays open.

The screenshots are not in this repository: the printed page, and therefore
the screenshots, showed network credentials.

## What this run does not establish

- **A start at boot.** The service was started by hand with the adapter down.
  A reboot during a WLAN outage is the case this was built for, and it was not
  run.
- **A printer that is silent at startup.** The printer answered the first
  query, so the retry schedule was exercised only by the tests.
- **The link-local and tentative cases.** Neither appeared on the adapter this
  time; both are exercised only by the tests.
- **An adapter absent altogether**, which is refused by design when the
  configuration is loaded, and was not tried.

## Prediction record

Before the run, Claude predicted:

- `Start-Service` would return without an error, and `sc.exe query` would show
  `STATE : 4 RUNNING` with exit code 0. **Held.** The bracketed timing did not
  time the start: Edwin ran a plain `Start-Service` first, which started the
  service, and the bracketed one found it already running. The log's
  21:39:47Z start line dates it.
- The log would end in one `Waiting for the printer-side interface` warning
  naming `Wi-Fi 2` as not up, or as having no IPv4 address, with nothing after
  it. **Held**, "not up".
- Within about 5 to 10 seconds of reconnecting, the log would show the wait
  ending, the `printer interface:` line, the announcement and the listener.
  **Held**, at 5 and 8 seconds.
- A second `Waiting for` line, or a `did not answer at startup` line, might
  appear, and Claude said it could not predict either. **Neither appeared.**
