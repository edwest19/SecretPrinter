# The printer-side WLAN drops, and nothing reconnects it

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21, from measurements taken on 2026-09-20 and 2026-09-21. Reviewed
by a human before merge.*

**Status: measured on FIOS-STB-01 with two different wireless adapters. Why the
link drops is NOT established. That nothing brings it back is measured for one
drop and consistent with the others. SecretPrinter cannot fix this and does not
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

## 2026-09-21: the Broadcom adapter, measured in the service log

The event log was **not** read for these drops, so the reason codes are not
known. What the service log and adapter state show:

- The adapter was down at 06:06Z (jobs failed `adapter 'Wi-Fi' is not up`) and
  again at 18:32Z. At about 19:25Z `Get-NetAdapter` reported it `Disconnected`,
  holding `192.168.12.186` as `Deprecated`. Whether it came back at all between
  06:06 and 19:25 is not known.
- At 19:46:00Z it was reconnected by hand. By 19:54:23Z it had dropped again:
  lookups failed `adapter 'Wi-Fi' is not up`, and `Get-NetAdapter` reported it
  `Disconnected` shortly after. It stayed disconnected until it was reconnected
  by hand again for the next install. **The Broadcom adapter held the link for
  about eight minutes.**
- A deliberate `netsh wlan disconnect` at 19:31:57Z, used to test the
  withdrawal, is not counted here as a drop.

The last few seconds before that 19:54 drop matter to anyone reading the service
log. Between 19:53:52Z and 19:54:12Z the adapter still reported up, and lookups
were answered from cache, but ten connections to the printer never completed.
The link was already failing before Windows said so. Those ten jobs ended with
no log line at all; that is
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
- **Why nothing reconnects.** For the 2026-09-20 drop, the event log shows
  AutoConfig never tried. For the 2026-09-21 drops, the event log was not read.
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
