# A lost printer-side membership, recovered without a restart

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-27. Reviewed by a human before merge.*

**Status: measured on hardware (FIOS-STB-01, `76330ce` as LocalService,
printer side on the Broadcom 802.11n adapter, `WI-FI`, index 10). With the
service running, the printer-side adapter was disabled and then enabled again.
When the adapter came back and connected, the service's membership of
`224.0.0.251` on it was gone: the count on the adapter read 1 until the
service's next retry, and 2 from the moment that retry reopened the socket. The
new socket heard the printer at once, and the advertisement was restored with
no restart. This is the recovery `REQ-RES-009` was built for, seen on hardware
for the first time. The loss was caused on purpose. A natural outage earlier the
same evening did not cause one. No code changes here.**

All times are UTC, 2026-09-27.

## Why this was measured

[`2026-09-25-a-reconnect-did-not-restore-the-membership.md`](2026-09-25-a-reconnect-did-not-restore-the-membership.md)
built `REQ-RES-009`: while the printer is held unreachable, the service reopens
its printer-side socket before each question, so that a membership lost during
an outage is joined again. Until this evening it had run on hardware only
through outages in which the membership was not lost. The overnight sampler in
[`2026-09-27-the-printer-went-silent-and-the-join-held.md`](2026-09-27-the-printer-went-silent-and-the-join-held.md)
saw no loss in three withdrawals, and a natural drop this evening, below, did
not lose it either. Waiting for a natural loss had no end in sight, so one was
caused on purpose.

## The switch back to the Broadcom

Edwin moved the printer side from the Linksys back to the Broadcom, which had
been dropping often, to test outages on it.

- The overnight sampler was stopped first. Its last sample was at 17:47:11.
  Over the whole file it held 15,896 samples from 19:07:35 on 2026-09-26 to
  17:47:11, with no gap over 15 s and none other than `13=2  17=2`. That covers
  the stretch from 16:30:49 to 16:44:00 that the 2026-09-27 finding lists as not
  read.
- The service stopped cleanly at 17:47:31.
- The Linksys was renamed `Linksys WUSB54GC` and disabled (index 13). The
  Broadcom was enabled and named `WI-FI`: index 10, `192.168.12.186`.
- The service started at 17:51:22: `printer adapter  : WI-FI` and
  `printer interface: WI-FI -> 192.168.12.186 (index 10)`, no wait, announced
  at 17:51:25.
- Both connections, `WI-FI` and `Ethernet`, were in the `Private` network
  category, which the firewall rules require. `224.0.0.251` read 2 on index 10
  and 2 on index 17.
- A new sampler, the same loop reading index 10 in place of 13 and writing to a
  new file, started at 17:58:13. Its samples below are about 5 s apart.

## A natural drop: the membership came back with the link

| Time | What |
|---|---|
| 18:22:58 | WLAN `8003`: disconnected by the driver. |
| 18:23:01 | `10=1`, the first sample after the drop. `netsh` still lists the interface. |
| 18:24:13 | Printer unreachable; advertisement withdrawn; listener closed. |
| 18:24:14 | `Could not reopen the printer-side socket: Interface 'WI-FI' holds 192.168.12.186 but is not up.` |
| 18:40:06–07 | Edwin reconnects by hand: `8000`, `8001`, both a manual connection. |
| **18:40:13** | **`10=2`**, 6 s after the `8001`. The service had not yet reopened anything. |
| 18:42:09 | The next retry: `Printer-side socket reopened on WI-FI (192.168.12.186, IPv4 index 10), and 224.0.0.251 joined again.` and `Printer reachable again`. |
| 18:42:11 | Advertisement restored. |

While the service was withdrawn, Edwin's iPhone listed no AirPrint printers. When
he looked is not recorded, only that it was after 18:24:13.

**What this shows.** While the link was down, one of the two memberships on the
adapter was gone within 3 s of the disconnect, and it was back within 6 s of the
reconnect, with no action by the service. The reopen at 18:42:09 therefore
replaced a socket whose membership had already returned. Whose membership went
and came back, the service's or the other process's, is not established: the
count is the same either way.

## The loss caused on purpose

**The test:** with the service running, disable the printer-side adapter so
that its interface goes away, then enable and reconnect it. Does the service
get back to the printer by itself, and was its membership really lost? The
control was `Ethernet`, index 17, at 2 throughout. A restart or a gap in the
samples would have voided it. Neither happened.

The commands, run by Edwin:

```powershell
Disable-NetAdapter -Name 'WI-FI' -Confirm:$false
Enable-NetAdapter -Name 'WI-FI' -Confirm:$false; Start-Sleep -Seconds 10; netsh wlan connect name="<profile>" interface="WI-FI"
```

| Time | What |
|---|---|
| 18:54:50 | `Disable-NetAdapter`. WLAN `8003`, "disconnected by the driver", in the same second. The adapter reads `Disabled`, index 10. |
| 18:54:55 | `10=` empty: `netsh` no longer lists the interface. |
| 18:55:03–21 | Four lookups on the old socket fail at once: `SocketException: The requested address is not valid in its context.` |
| 18:55:26 | Printer unreachable; advertisement withdrawn; listener closed, 36 s after the disable. |
| 18:55:26 | `Could not reopen the printer-side socket: Interface 'WI-FI' has no IPv4 address, so it cannot be used.` Logged once. |
| 18:55:26–18:56:29 | Questions through the old socket at 18:55:26, :27, :29, :33, :41, :57 and 18:56:29, each failing at once. |
| 18:56:52 | `Enable-NetAdapter`. The adapter comes back on index 10. |
| 18:57:03 | `netsh wlan connect`: WLAN `8000` and `8001`, a manual connection. |
| **18:57:04–29** | **`10=1`**, six samples. |
| 18:57:33 | The next retry: `Printer-side socket reopened on WI-FI (192.168.12.186, IPv4 index 10), and 224.0.0.251 joined again.` and `Printer reachable again`, in the same second. |
| **18:57:34** | **`10=2`**, and from then on. |
| 18:57:35 | Advertisement restored. |

The sampler held 733 samples from 17:58:13 to 19:00:49, with no gap over 15 s
and `17=2` in every one.

The questions while the adapter was away failed at once rather than waiting out
the resolve timeout, so the retry schedule ran without its usual 5 s per
question. The intervals were 1, 2, 4, 8, 16 and 32 s, and the reopen that
succeeded came 64 s after the last failure, at the next step.

**What this establishes:**

- **After the adapter was disabled and enabled, the service's old socket held no
  membership.** The count read 1 after the reconnect. The reopen at 18:57:33
  added the new socket's join and closed the old socket, and the count became 2.
  Had the 1 been the old socket's membership, closing that socket would have left
  the count at 1. This assumes the other process's count did not change in the
  same moment, the same assumption the 2026-09-25 finding makes.
- **The reopen restored the membership and the printer was heard through it at
  once,** with no restart. That is `REQ-RES-009` recovering from a real loss on
  hardware.
- **The service's view of the adapters still lists a disabled adapter by name,**
  with no IPv4 address, so the reason logged is `has no IPv4 address` rather than
  `No interface is named`.

**What it does not establish:**

- **What lost the membership on 2026-09-25,** after a natural outage. Tonight's
  natural outage of 17 minutes did not lose it, and neither did those of 17 and
  33 minutes on 2026-09-25. Whether a disable and enable is the same mechanism is
  not known.
- **Whose membership goes during a natural outage** while the link is down.
- **That the old socket was deaf by test.** It was not asked anything between the
  reconnect and the reopen, so its deafness in those 30 s rests on the count,
  not on an unanswered question.

## Predictions

Held: each drop's withdrawal within about a minute and a half; `10=` empty after the disable; the
`8000` and `8001` at each reconnect; the retry after the natural drop at about
18:42:06 (it came at 18:42:09); a weak lean toward the membership coming back
with the link after the natural drop; a weak lean toward a real loss after the
disable.

Missed:

- Claude predicted that the startup line `printer adapter  :` would show the
  configuration's spelling. It showed `WI-FI`, the adapter's own name. The
  configuration loader matches the name without regard to case and keeps the
  adapter's name as Windows reports it.
- After the disable, Claude predicted `No interface is named 'WI-FI'`. The
  message was `Interface 'WI-FI' has no IPv4 address, so it cannot be used.`
- Claude's adapter list before the switch was stale: Edwin had already made the
  swap.
