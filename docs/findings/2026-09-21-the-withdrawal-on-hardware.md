# The withdrawal and recovery, run on hardware for the first time

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21. Reviewed by a human before merge.*

*Corrected 2026-09-21, the same day, by Claude (Anthropic model, Claude Opus 5),
at the direction of Edwin West. "Loss, by the adapter's own drop" first dated the
drop to between 19:54:12Z and 19:54:23Z, inferred from the service log. The
WLAN-AutoConfig log records it at 19:53:55Z. The section is rewritten from the
event log, and the one later mention of the drop's time is corrected to match.
Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01. The service withdrew when the printer stopped
answering and came back when it answered again, in the order `REQ-LIF-006`
requires and on the schedule `REQ-RES-008` gives, against both a deliberate
disconnect and a real WLAN drop. Four gaps were found and are listed at the end;
none was fixed in this run. The most visible: the iPhone kept listing the printer
after the goodbye.**

## The setup

FIOS-STB-01, running `1dc116c` as `LocalService`. Printer side: the Broadcom
802.11n adapter (`Wi-Fi`, index 9), chosen because it drops often
([`2026-09-20-the-wlan-drops-and-nothing-retries.md`](2026-09-20-the-wlan-drops-and-nothing-retries.md)).
Client: the iPhone at `192.168.1.152`. Before the run, the iPhone listed
`SecretPrinter (ET-3760)` once and showed supply levels through it.

The withdrawal and recovery times for the deliberate run were predicted from the
code before they were measured.

## Loss, by deliberate disconnect

| UTC | Log |
|---|---|
| 19:31:57 | `netsh wlan disconnect interface="Wi-Fi"` |
| 19:31:58 | `Job: failed for 192.168.1.152:52735: Relay ended early while reading from the printer: … forcibly closed by the remote host.` (46,254 bytes sent, 38,256 received, 140.13 s) |
| 19:33:12 | `Printer unreachable: it stopped answering on the printer-side interface and its records have expired. …` |
| 19:33:12 | `No longer accepting print jobs on Ethernet.` |
| 19:33:12 | `Advertisement withdrawn and the listener closed: …` |

- **75 seconds from disconnect to withdrawal**, inside the predicted 24–120 s:
  the record dies 120 s after the printer's last answer, and where the
  disconnect falls in that window sets the delay.
- **The gate closed before the goodbye went out**, as `OfferAsync` orders it.
- **No `Printer lookup failed …` warning appeared.** The reconfirmation queries
  fell inside the logged window, so on this adapter a send over a disconnected
  WLAN did not throw. This run therefore did *not* exercise the path `1dc116c`
  fixed; that fix is still untested on hardware.
- **Connection `52735` was cut by the disconnect**, one second after it. Windows
  words any reset as *forcibly closed by the remote host*; it does not show that
  the printer sent the reset. That same text on two earlier jobs has been
  unexplained; a WLAN drop is now a demonstrated way to produce it. That is a
  candidate explanation for those two, not an established one.

## Recovery

| UTC | Log |
|---|---|
| 19:46:00 | `netsh wlan connect` |
| 19:51:06 | `Printer reachable again: it answered on the printer-side interface.` |
| 19:51:07 | `Accepting print jobs on 192.168.1.161:631 (Ethernet); …` |
| 19:51:08 | `Advertisement restored: the printer is on offer again.` |

- **Predicted 19:51:05, measured 19:51:06.** After the loss the watch queries at
  once and then after gaps of 1, 2, 4, 8… seconds, and each unanswered query
  first waits the configured 5 s resolve timeout. That puts the queries at
  about 19:42:28 — while the WLAN was still down — and 19:51:05.
- **The listener reopened on the same address and port.** This is the first
  execution anywhere of `RunRelayAsync`'s close-and-reopen loop; no test covers
  it.
- **The gate opened, then the listener came up, then the announcement.**

## Loss, by the adapter's own drop

The WLAN-AutoConfig log records the Broadcom's driver disconnecting at
**19:53:55Z**. At **19:54:52Z**, 57 seconds later, the service declared the
printer unreachable, withdrew and closed its listener. The withdrawal and
listener lines appeared in the other order this time, which the code allows.

Connections arriving between the drop and 19:54:52 were accepted. Those whose
lookup was answered from cache, between 19:53:52Z and 19:54:12Z, ended with no
log line at all — the defect in
[`2026-09-17-connect-timeout-is-not-reported.md`](2026-09-17-connect-timeout-is-not-reported.md),
fixed in `d29dd4e`. Those that had to query, from 19:54:23Z, failed with the
adapter named. Accepting during that window is designed: the service condemns
the printer when its record expires, not on the first silence.

## The gaps

**1. The iPhone kept the printer listed after the goodbye.** After the 19:33:12
withdrawal the entry stayed in the iPhone's list; tapping it failed, since
nothing was listening. The code suggests why, but it is **not established**:
announcements and goodbyes go out over IPv4 only — deliberately, per a comment in
`MdnsResponder`, pending "a separate, measured decision" — while iOS asks only
over IPv6 and our answers go back over the family the question arrived on. The
record that lists the printer is a PTR with TTL 4500 s, which the withdrawn
responder will not refresh. If iOS does not apply an IPv4 goodbye to records it
learned over IPv6, the entry would stay for up to 75 minutes. How long it
actually stayed was not measured, because recovery was tested next.

**2. A job cannot bring the service back from a withdrawal.** `REQ-RES-008` says
a lookup made because a job arrived resets the schedule. While withdrawn, the
listener is closed, so no job can arrive. The backoff is the only way back, and
it reaches one query an hour.

**3. The service will not start while the printer's adapter is down.** Startup
resolves the configured interface and refuses one that is not up. `REQ-LIF-006`
covers the printer going away after startup only, so a reboot during a WLAN
outage leaves the service stopped rather than withdrawn. Because of the
still-unrecorded exit-code defect, that refusal reaches the service control
manager as error 1053, not a readable reason.

**4. Connections cut by the withdrawal are not reported.** Recorded in
[`2026-09-17-connect-timeout-is-not-reported.md`](2026-09-17-connect-timeout-is-not-reported.md)
under "Still open".

## What this run does not establish

- **A page printed after an in-service recovery.** Nothing relayed between the
  19:51 recovery and the 19:53:55 drop. Printing on the next build, after a restart,
  is shown in
  [`2026-09-18-the-pin-refuses-a-mismatch.md`](2026-09-18-the-pin-refuses-a-mismatch.md);
  printing after the service itself recovered is still to be seen.
- **The withdrawal against the older "adapter up but deaf" failure.** Both drops
  here took the adapter down.
