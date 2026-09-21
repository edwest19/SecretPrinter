# The printer-side WLAN drops, and nothing reconnects it

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21, from measurements taken on 2026-09-20 and 2026-09-21. Reviewed
by a human before merge.*

*Corrected 2026-09-21, the same day, by Claude (Anthropic model, Claude Opus 5),
at the direction of Edwin West, after the WLAN-AutoConfig log was read for the
Broadcom adapter. The first version dated the Broadcom's own drop to "by
19:54:23Z" and said the adapter "still reported up" while ten connections went
silent. Neither had been measured: both were inferred from the service log. The
event log puts the drop at 19:53:55Z, before most of those connections. The
section on 2026-09-21 is rewritten from the event log, and the status line and
"Why nothing reconnects" are updated. Reviewed by a human before merge.*

**Status: measured on FIOS-STB-01 with two different wireless adapters. Why the
link drops is NOT established. That nothing brings it back is measured on both
adapters: after every drop the driver initiated, the event log records no
reconnect attempt until a person made one. SecretPrinter cannot fix this and does not
try to; `REQ-LIF-006` exists so that the service stops offering a printer it
cannot reach while the link is down.**

## The machine

FIOS-STB-01 reaches the printer network over Wi-Fi, profile `TMOBILE-9992`, and
the client network over wired Ethernet (`Ethernet`, index 15). Two wireless
adapters have been used on the printer side:

| Adapter | Name in Windows | Index | Used |
|---|---|---|---|
| Compact Wireless-G USB Adapter | `Wi-Fi 2` | 49 | until 2026-09-20 |
| Broadcom 802.11n Network Adapter | `Wi-Fi` | 9 | from 2026-09-21, chosen *because* it drops often, so faults could be tested |

`netsh wlan show profile` reports the profile as **Connect automatically**.

## 2026-09-20: the USB adapter, measured in the event log

Local times are EDT; the service log stamps UTC.

| UTC | Event |
|---|---|
| 06:49:28 | WLAN-AutoConfig `11004`: wireless security stopped |
| 06:49:38 | WLAN-AutoConfig `8003`: disconnected — *the network is disconnected by the driver* |
| *(9 h 05 m)* | **no WLAN-AutoConfig events at all**: no `8000`, no `8002`. Nothing attempted a reconnect. |
| 15:29:13–15:30:14 | 12 jobs accepted, 12 failed, each diagnosed `adapter 'Wi-Fi 2' is not up` |
| 15:54:46 | manual `netsh wlan connect`: associated, secured and connected within one second, 99% signal |
| 16:23:13 | first job after the reconnect: `located … (queried, 0.018s)`, relayed, printed |

That run was on `53f001c`, before `REQ-LIF-006`, which is why jobs went on
being accepted for nine hours with no printer behind them.

## 2026-09-21: the Broadcom adapter, measured in the event log

WLAN-AutoConfig events `8000` (connection started), `8001` (connected), `8002`
(connection failed) and `8003` (disconnected), for the whole day:

| Local (EDT) | UTC | Event |
|---|---|---|
| 00:08:42 | 04:08:42 | `8000` started |
| 00:10:45 | 04:10:45 | `8002` failed |
| 00:21:29 | 04:21:29 | `8000` started |
| 00:22:22 | 04:22:22 | `8001` connected |
| 01:57:55 | 05:57:55 | `8003` — *the network is disconnected by the driver* |
| *(13 h 29 m)* | | **no `8000`, `8001` or `8002`: nothing tried to reconnect** |
| 15:26:50 | 19:26:50 | `8000`, `8001`: connected by hand, for the install |
| 15:31:58 | 19:31:58 | `8003` — *disconnected by the user*: the deliberate withdrawal test, not a drop |
| 15:46:00–01 | 19:46:00–01 | `8000`, `8001`: connected by hand |
| 15:53:55 | 19:53:55 | `8003` — *the network is disconnected by the driver* |
| *(22 m 50 s)* | | **nothing tried to reconnect** |
| 16:16:45 | 20:16:45 | `8000`, `8001`: connected by hand, for the next install |

How the connections just after midnight were started is not recorded here.

So the Broadcom held the link for **1 h 35 m** overnight and for **7 m 54 s**
in the afternoon, and both times the driver ended it and nothing brought it back.
The service log agrees: the jobs that failed `adapter 'Wi-Fi' is not up` at
06:06Z and 18:32Z fall inside the 13-hour gap.

The afternoon drop at 19:53:55Z matters to anyone reading the service log.
Ten connections from the iPhone were accepted between 19:53:52Z and 19:54:12Z;
eight of them arrived *after* the drop. Each logged `located … (cached, 0s)`,
because a lookup answered from cache does not consult the adapter, so the log
shows a printer being located that could no longer be reached. The first
`adapter 'Wi-Fi' is not up` appeared at 19:54:23Z, when a lookup next had to
query. Those ten jobs then ended with no log line at all; that is
[`2026-09-17-connect-timeout-is-not-reported.md`](2026-09-17-connect-timeout-is-not-reported.md),
fixed in `d29dd4e`.

## The multicast membership survived a reassociation

On 2026-09-20 the service was never restarted: the whole log file holds one
`Service 'SecretPrinter' starting` line, at 2026-09-19 20:25:44Z. Yet the first
lookup after the manual reconnect was a fresh query answered in 18 ms. So the
socket's membership of `224.0.0.251` on that adapter carried through the drop and
the reassociation.

That cuts against reassociation as the explanation for the older failure mode
recorded in
[`2026-09-19-the-printer-side-multicast-membership-is-lost.md`](2026-09-19-the-printer-side-multicast-membership-is-lost.md),
where the adapter stayed up but no multicast answers arrived. On this evidence
the "adapter up but deaf" mode and the "adapter down" mode may not share a
cause. That is weakened, not settled.

## Three explanations tested, and all three failed

- **The profile is set to connect manually.** It is not: `netsh wlan show
  profile` reports *Connect automatically*. The `8003` event's field
  `Connection Mode: Manual connection with a profile` describes how *that
  connection* was started — by the earlier manual `netsh wlan connect` — not the
  profile's setting. Claude first read it the other way, and was wrong.
- **The USB adapter fell off the bus.** The System log has nothing between 01:19
  and 09:19 local on 2026-09-20: no Kernel-PnP, no device events. That is weak
  evidence of absence, since that driver may not log to System at all.
- **USB selective suspend powered it down.** `Get-NetAdapterPowerManagement`
  reports `SelectiveSuspend : Unsupported`.

## What is not established

- **Why the link drops.** An access point deauthenticating an idle client would
  produce the same `8003` line as a driver fault. The Device Manager power setting
  ("Allow the computer to turn off this device to save power") is a PnP setting,
  separate from NDIS selective suspend, and was never looked at. Since the drops
  continued on a second adapter from a different maker, the access point, the
  profile or the machine are as likely as either driver — but nothing here
  measures that.
- **Why nothing reconnects.** For every driver-initiated drop measured — one
  on the USB adapter, two on the Broadcom — the event log shows AutoConfig never
  tried, although the profile is set to connect automatically. Why it does not
  is not known.
- **Where to look.** On FIOS-STB-01, the WLAN-AutoConfig operational log is the
  only channel found so far that records these drops at all. The service log
  records their effect, not their cause.

## What SecretPrinter does about it

It does not reconnect the WLAN, and it will not: `REQ-SEC-007` forbids the
service from changing host network configuration. What it does is stop offering the
printer while it cannot reach it, and resume when it can (`REQ-LIF-006`). That
ran on this machine for the first time on 2026-09-21, against both a deliberate
disconnect and one of these real drops; the run is recorded separately.

Until the drops are explained, a machine like this one needs its printer-side
link watched, and reconnected by a person when it goes.
