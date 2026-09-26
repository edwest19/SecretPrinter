# A reconnect did not restore the printer-side membership

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-25. Reviewed by a human before merge.*

**Status: measured on hardware (FIOS-STB-01, running `523bacd` as LocalService,
printer side on the Broadcom `Wi-Fi`). The resolver socket's membership of
`224.0.0.251` on the printer-side adapter was found gone after two outages in a
row, and the adapter reconnecting did not bring it back. Only a new socket
joined the group again. The repair, `REQ-RES-009`, is built in the commit that
adds this finding. It has not yet run on hardware.**

*(Status updated 2026-09-26 by Claude, Claude Opus 5.5: the repair is installed
on FIOS-STB-01 and has run there once, through a deliberate outage. It
withdrew, logged once why it could not reopen, and after the reconnect reopened,
heard the printer and restored, with no restart. The join did not go missing in
that outage, so recovery from a lost membership itself is still to be seen. See
"The repair on hardware" below.)*

This is the mechanism recorded in
[`2026-09-19-the-printer-side-multicast-membership-is-lost.md`](2026-09-19-the-printer-side-multicast-membership-is-lost.md),
measured a second time on a different adapter, after a real outage and with a
control. That finding also showed that a service whose own membership is gone
cannot hear the printer, even while another process on the machine still holds
a membership of the same group.

All times are UTC, 2026-09-25.

## The Wi-Fi's day

From the WLAN-AutoConfig log (`8001` connect, `8003` disconnect), read from
about 01:35Z on. Every disconnect in that time gave the reason "The network is
disconnected by the driver", and no `4003` appeared.

| Down | Back | Out for | Reconnected by |
|---|---|---|---|
| before 01:35 (not read) | 01:50:54 | — | Edwin, by hand |
| 02:54:59 | 02:57:58 | 2 min 59 s | no hand reconnect is recorded |
| 03:39:56 | 03:41:19 | 1 min 23 s | no hand reconnect is recorded |
| 07:06:00 | 17:23:02 | 10 h 17 min | Edwin, by hand |
| 17:23:38 | 17:27:00 | 3 min 22 s | Edwin, by hand; the connect before lasted 36 s |
| 17:46:00 | 18:02:52 | 16 min 52 s | Edwin, by hand |

## What the service did

From its log. Starts and stops are Edwin's.

| Time | Event |
|---|---|
| 01:53:31 | start |
| 02:26:22 → 02:26:37 | withdrawn, restored (no WLAN event) |
| 02:56:10 → 02:58:55 | withdrawn, restored |
| 03:17:10 → 03:23:33 | stop, start |
| 03:40:09 → 03:41:45 | withdrawn, restored |
| 03:43:44 → 03:44:09 | withdrawn, restored (no WLAN event) |
| 07:07:10 | withdrawn |
| 17:31:02 → 17:32:29 | stop, start (the measurement below) |
| 17:47:27 | withdrawn |
| 18:05:20 → 18:05:22 | printer reachable again, restored |

## The measurement

`netsh interface ipv4 show joins` counts the memberships of each group on each
interface. `Ethernet` (index 17) is the client side and serves as the control.
`Wi-Fi` (index 10) is the printer side.

| When | Service | `Wi-Fi` state | `224.0.0.251` on `Wi-Fi` | on `Ethernet` |
|---|---|---|---|---|
| after 17:27:00, before 17:31:02 | running since 03:23:33, withdrawn since 07:07:10 | connected | **1** | 2 |
| after `Stop-Service` at 17:31:02 | stopped | connected | **1** | 1 |
| 5 s after `Start-Service` at 17:32:29 | running | connected | **2** | 2 |
| 18:05:11 | running since 17:32:29, withdrawn since 17:47:27 | connected | **2** | 2 |

Read in order:

- **Stopping released nothing on `Wi-Fi`.** Ethernet went from 2 to 1, so the
  stop did release the service's joins. `Wi-Fi` stayed at 1. So before the
  stop, the service held no membership on `Wi-Fi`; the one left belongs to
  another process. This assumes that process's count did not change in the
  same moment.
- **Starting added it back.** A new socket joined at once, and `Wi-Fi` read 2.
  That is this adapter's baseline, the same as the Linksys's on 2026-09-19: the
  service holds one join per interface.
- **A 17-minute outage did not lose it.** At 18:05:11 the count was 2, with the
  service withdrawn and not restarted. The service restored itself at
  18:05:20Z, on its scheduled query, at the second Claude had predicted from
  the schedule's code.

## What this does not establish

- **Which outage lost the membership.** Between the last moment it is known to
  have been there and the reading, the adapter was out twice: for 10 h 17 min
  from 07:06:00, and, after a reconnect that lasted 36 s, for 3 min 22 s from
  17:23:38. Either could have lost it.
- **Why it was lost,** or whether an outage's length decides it. On the same
  day it survived outages of 16 min 52 s (measured), 2 min 59 s and 1 min 23 s.
  The last two are inferred from the service hearing the printer again
  afterwards, which the 2026-09-19 finding shows it cannot do without its own
  membership.
- **What the other process holding a membership is.**

## Predictions that missed

- After Edwin's first reconnect, Claude predicted the alive check would show
  the Wi-Fi connected and the printer reachable. Both were false: the link had
  dropped again, 36 seconds after that reconnect.
- Claude predicted `Wi-Fi` would read 2 after the long outage. It read 1.
- Claude leaned toward 1 after the 17-minute outage, weakly, saying so. It read
  2.

A correction too. During the session Claude told Edwin the membership "was gone
after the 10-hour" outage. The 3-minute outage at 17:23:38 also came before the
reading, so that was not established. It is stated correctly above.

## The repair: REQ-RES-009

The 2026-09-19 finding named the likely shape: rebuild the resolver socket
rather than try to detect a loss that cannot be seen from managed code. On
2026-09-25 Edwin West chose when. The socket is reopened before every question
put to a printer that is held unreachable, and before every startup attempt
after the first. The other trigger considered was Windows reporting an address
change. It was not chosen, because it depends on an event not measured on
FIOS-STB-01, and adapter state has been observed wrong in both directions on
this project.

**What reopening is.** `PrinterSide` holds the printer-side socket and the
resolver on it. `Reopen` examines the printer adapter again by the rule the
startup wait uses. If the adapter is usable, it opens a new socket joined to
`224.0.0.251` on the adapter's address as it is now, and starts a new resolver
on it. Only then does it close the old pair. If the adapter is not usable, or
the socket cannot be opened, the old pair is kept.

**Where it happens.**

- `ServiceHost.AskForWatchAsync` is the watch's question. It reopens first
  when the printer is held unreachable.
- `ServiceHost.AskAtStartupAsync` is one startup attempt. It reopens first
  after an unanswered attempt.
- Print jobs read the resolver from `PrinterSide` for each connection, so they
  always ask through the current one.

**What it logs.** A failure to reopen is logged when its reason first appears,
and again only when the reason changes. A successful reopen is logged only after
a logged failure, or when it lands on a different address or index. A routine
reopen logs nothing. While the printer is away that happens after 1, 2, 4, 8…
seconds and then hourly, and a line each time would bury the lines that matter.

**What it covers.**

- **A membership lost during an outage:** the first question after the link
  returns goes out on a new socket and is heard. After a long outage the
  schedule has backed off to hourly, so that can still be up to an hour after
  the link returns. Before this change it was never, without a restart.
- **A membership lost while the printer is held reachable,** with the adapter
  up and only the group gone, the 2026-09-19 case: the reconfirmations go
  unheard and the record expires, which withdraws the printer. The watch then
  asks at once, reopening first, and the printer is back within seconds. The
  withdrawal is the cost.
- **An address that changed while the adapter was out:** the new socket joins
  on the new address. That is the rebinding the 2026-09-18 finding asked for.

**What it does not do.** It does not detect a loss. It does not reconnect the
Wi-Fi, which `REQ-SEC-007` forbids. A print job's lookup never reopens anything.

**Tests.**

- Five in `PrinterSideTests`:
  - a reopen puts a new pair in place and closes the old one;
  - an unusable adapter keeps the old pair and is logged once;
  - a socket that cannot be opened keeps the old pair and says why;
  - a recovery after a logged failure is logged, and a routine reopen is not;
  - a move to a new address says where it was and where it is.
- Two in `ServiceHostTests`:
  - the watch asks an unreachable printer through a reopened socket, and a
    reachable one through the same socket, from the network rather than the
    cache;
  - a startup attempt after an unanswered one reopens, and the first does not.

With each reopen call removed on purpose, and separately with the closing of
the old pair removed, the test that checks it failed. These tests use fake
sockets. Whether a real new socket rejoins on Windows is the measurement above,
not something they show.

**Not yet run on hardware.** FIOS-STB-01 runs `523bacd`. Verifying the repair
takes three things: this build installed there, an outage that loses the
membership, and the service coming back after the reconnect without a restart.
While it is still withdrawn, `Wi-Fi` should read 2 again after the first query
that follows the reconnect.
*(Added 2026-09-26: installed, and run once. See "The repair on hardware"
below.)*

## The repair on hardware

*Added 2026-09-26 by Claude (Anthropic model, Claude Opus 5.5) at the direction
of Edwin West. Reviewed by a human before merge.*

**Installed.** `76330ce` was published to `C:\Program Files\SecretPrinter\`
on FIOS-STB-01 by the procedure in `docs/operating.md`, pulling first. The two
DLLs checked were written at 23:15:48Z and 23:15:50Z on 2026-09-25, and the
service started at 23:16:54Z on the same adapters as before. The Wi-Fi dropped
while startup waited for the printer. Edwin reconnected it and restarted the
service, and it announced at 23:22:04Z. With the new build running, `Wi-Fi` and
`Ethernet` each read 2 for `224.0.0.251`.

**The run.** It tested whether, with the printer-side adapter taken away and
given back, the new build withdraws, says once why it cannot reopen, and after
the reconnect reopens, hears the printer and restores, all with no restart.
Edwin disconnected and reconnected the Wi-Fi by hand, with
`netsh wlan disconnect` and `netsh wlan connect`.

| Time (UTC) | What happened |
|---|---|
| 2026-09-25 23:25:51 | Wi-Fi disconnected by hand |
| 23:27:16 | printer unreachable, listener closed, advertisement withdrawn |
| 23:27:17 | `Could not reopen the printer-side socket: Interface 'Wi-Fi' holds 192.168.12.186 but is not up.` |
| 23:59:10 | Wi-Fi reconnected by hand |
| 2026-09-26 00:02:21 | `Printer-side socket reopened on Wi-Fi (192.168.12.186, IPv4 index 10), and 224.0.0.251 joined again.` |
| 00:02:21 | printer reachable again; accepting print jobs |
| 00:02:23 | advertisement restored |

The warning was written once, although a reopen was tried, and failed, before
every retry while the adapter was out: 33 min 19 s from the disconnect to the
reconnect. The restore came 3 min 13 s after the reconnect, at the first retry
the backoff allowed.

**Predictions.** Claude predicted the withdrawal by about 23:28Z, and it came
at 23:27:16Z. It predicted the first retry after the reconnect at 00:02:19Z,
from the schedule's code, and it came at 00:02:21Z. It predicted the restore
line before `Accepting print jobs`. The order was the other way: the listener
opened at once, and the restore was logged 2 s later, when the announcements
had gone out.

**What it shows.**

- On Windows, a reopen can fail with Windows' own reason and be reported once.
- A reopen can then succeed after the adapter returns, and the printer is heard
  through the new socket.
- None of it needed a restart.
- The old socket's sends did not fail while the adapter was down. No "lookup
  failed before an answer could be expected" warning was logged; each question
  waited out its timeout instead.

**What it does not show.** Recovery from a lost membership. After the
reconnect, and before the reopen, `Wi-Fi` read 2: the old socket's join had
come through this 33-minute disconnect. That adds a deliberate 33-minute outage
to the ones the join survived. So in this run the reopen replaced a socket that
could still hear. Recovery from an actual loss waits for one to happen with
this build installed.

## Side observations

- **Two withdrawals with no WLAN event**, at 02:26:22Z (13 s) and 03:43:44Z
  (23 s). The adapter was connected throughout, as far as the WLAN log shows.
  The printer went unheard for a whole record lifetime and then answered. Why is
  not established.
- **Run 1's goodbye.** The goodbye in the burst that `Listen` read at
  02:53:24.5Z was the 02:26:22Z withdrawal, and the three announcements after
  it were the 02:26:37Z restore. The service sends goodbyes only on a stop or a
  withdrawal, and there was no stop in that gap. This assumes the two machines'
  clocks do not differ by minutes, which was not checked. A dated note in
  [`2026-09-25-the-iphone-drops-the-printer-on-the-goodbye.md`](2026-09-25-the-iphone-drops-the-printer-on-the-goodbye.md)
  records it.
- **WLAN-AutoConfig reconnected by itself twice and then not at all.** After
  02:54:59Z and 03:39:56Z the adapter was back within three minutes, with no
  hand reconnect recorded. After 07:06:00Z nothing reconnected it for ten hours,
  as in
  [`2026-09-20-the-wlan-drops-and-nothing-retries.md`](2026-09-20-the-wlan-drops-and-nothing-retries.md).
- **Two different BSSIDs.** The adapter's BSSID after the reconnect at 17:23Z
  differed from the one after 18:02Z, both on channel 3 with the same network
  name. Whether they are two access points or two radios of one is not known.
  The addresses are not recorded here.
