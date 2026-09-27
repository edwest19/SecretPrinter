# The printer went silent on a working link, and the join held

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-27. Reviewed by a human before merge.*

**Status: measured on hardware (FIOS-STB-01, `76330ce` as LocalService,
printer side on the Linksys Compact Wireless-G USB adapter, `Wi-Fi`, index 13).
A sampler recorded how many memberships of `224.0.0.251` each adapter held,
about every 5 seconds, from 19:07:35Z on 2026-09-26. The service withdrew three
times in that run, and the printer side read 2 in every sample, so none of the
three was a lost membership. The third withdrawal lasted 5 h 9 min 39 s. During
it the printer-side link, address, routes and gateway all worked, and nothing
at the printer's address answered ARP. After Edwin switched the printer off and
on, the service heard it at its next scheduled question and restored itself,
with no restart. Recovery from a lost membership is still not seen. No code
changes here.**

This file also records what was read from FIOS-STB-01's logs for 2026-09-26,
which no other finding holds: eleven withdrawals on the Linksys, two losses of
its address with no WLAN disconnect logged, a driver installed by Windows
Update three seconds before the first of those losses, and a service stop that
did not finish because the machine was switched off.

All times are UTC.

## Why this was measured

[`2026-09-25-a-reconnect-did-not-restore-the-membership.md`](2026-09-25-a-reconnect-did-not-restore-the-membership.md)
built `REQ-RES-009`: while the printer is held unreachable, the service reopens
its printer-side socket before each question, so that a membership lost during
an outage is joined again. On hardware it has run through one deliberate
outage, and the join survived that one, so the reopen replaced a socket that
could still hear. Recovery from an actual loss waits for one to happen.

On 2026-09-26 the service log showed eleven withdrawals on the Linksys, seven
of which ended within a second: the first question after the withdrawal, sent
on the freshly reopened socket, was answered. A socket that had lost its
membership would give that shape. So would a printer that went briefly unheard.
The log cannot tell the two apart. The membership count at the moment can.

## The machine

- **Service:** `76330ce`, installed on 2026-09-25 as the 2026-09-25 finding
  records. Nothing has been published to FIOS-STB-01 since. The DLL timestamps
  were not re-read.
- **Printer side:** the Linksys Compact Wireless-G USB adapter, named `Wi-Fi`,
  index 13, `192.168.12.136/24` from DHCP, 802.11g. Edwin disabled the Broadcom
  and moved the printer side to this adapter on 2026-09-26: the service stopped
  at 02:55:00 and the Linksys connected at 02:55:25. On 2026-09-19 and
  2026-09-22 this adapter was `Wi-Fi 2`, index 49. At the service's start at
  02:57:02 on 2026-09-26 it was already `Wi-Fi`, index 13. How it came to have
  the new name and index is not established.
- **Client side:** `Ethernet`, index 17, `192.168.1.161/24`.
- **The run:** the service started at 18:31:08 on 2026-09-26. Its log has no
  start line after that, up to 17:06:38 on 2026-09-27.

## 2026-09-26, from the logs

Read on 2026-09-26 from the service log and the WLAN-AutoConfig log (events
`4003` and `8000` to `8003`), and on 2026-09-27 from the System log and the list
of installed drivers.

| Time | What |
|---|---|
| 00:18:57 | The Broadcom, still the printer side then (`Wi-Fi`, index 10): `8003`, disconnected by the driver. Nothing reconnected it. |
| 00:20:36–37 | Withdrawn. `Could not reopen the printer-side socket: Interface 'Wi-Fi' holds 192.168.12.186 but is not up.` |
| 02:55:00 | Service stopped cleanly: `Service 'SecretPrinter' stopping.`, `SecretPrinter stopped.` |
| 02:55:25 | The Linksys (`Compact Wireless-G USB Adapter #2`) connects: `8000`, `8001`. |
| 02:57:02 | Service starts. `printer interface: Wi-Fi -> 192.168.12.136 (index 13)`, no wait. |
| 03:29:36 | System log `43`: Windows Update starts installing `INTEL - USB - 10/3/2016 12:00:00 AM - 10.1.1.38`. |
| 03:29:40 | System log `19`: installed. |
| 03:29:43–03:30:01 | Four printer lookups fail at once: `The requested address is not valid in its context.` No `8003`. |
| 03:30:05 | Withdrawn. The reopen fails: `Wi-Fi` holds `169.254.27.62` but is not up. |
| 03:35:31 | System log `1074`: `winlogon.exe` has initiated the power off of FIOS-STB-01 on behalf of `NT AUTHORITY\SYSTEM`; reason code `0x500ff`, for which Windows found no title; shutdown type power off. Edwin confirmed on 2026-09-27 that he switched the machine off and on. |
| 03:35:40 | `Service 'SecretPrinter' stopping.`, and System log `6006` (the event log service stopped) in the same second. No `SecretPrinter stopped.` |
| 03:37:07 | System log `6005`: the event log service started. |
| 03:37:22 | Service starts and waits: `Interface 'Wi-Fi' holds 169.254.27.62 but is not up.` |
| 03:46:44 | Linksys `8001`. |
| 03:46:47 | `printer interface: Wi-Fi -> 192.168.12.136 (index 13)`. |
| 03:58:29–11:33:38 | Eleven withdrawals, each ended by an answer (below). |
| 11:35:33 | Lookups fail at once again, address not valid. No `8003`. |
| 11:35:44 | Withdrawn. The reopen fails: `Wi-Fi` holds `169.254.27.62` but is not up. Each hourly question until 17:44:01 fails at once, the reason unchanged. |
| 18:30:57 | Linksys `8001`. |
| 18:31:08 | Service stopped and started in the same second, cleanly. Configuration logged at 18:31:09: `Wi-Fi` usable at once, index 13. |
| 18:31:14, 18:31:23 | The printer did not answer at startup; it answered after two unanswered attempts. Announced 18:31:25. |
| 19:14:06–09 | Three print-job connections completed through the relay. |

The eleven withdrawals, each with the time until `Printer reachable again`:

| Withdrawn | Reachable again after |
|---|---|
| 03:58:29 | 2 min 44 s |
| 04:16:28 | 0 s |
| 04:52:39 | 0 s |
| 05:01:28 | 0 s |
| 07:06:42 | 0 s |
| 07:15:13 | 1 s |
| 07:45:18 | 1 s |
| 08:41:33 | 36 s |
| 10:03:17 | 14 s |
| 10:07:14 | 0 s |
| 11:33:38 | 6 s |

In that period the log has no `Could not reopen` line, and the code writes one
the first time a reopen fails, so every reopen found the adapter usable. No
lookup failed with "address not valid". The WLAN-AutoConfig log has no event of
the kinds read between 03:46:44 and 18:30:55. The four slower recoveries fall
within 2 s of the retry schedule's questions 7, 4, 2 and 1. The sampler was not
running, so whether these were a socket that had lost its membership, a printer
that went unheard, or a link fault the WLAN log did not record is not
established.

What the day does establish:

- **On this adapter, a quiet WLAN log does not show that the link stayed up.**
  It lost its address twice with no `8003`. The second time, it was still
  unusable at the service's question at 17:44:01, and the next WLAN event was
  the `8001` at 18:30:57, 6 h 55 min after the loss. What made that connection
  is not established.
- **The first address loss began 3 s after Windows Update finished installing a
  driver on the USB host controllers.** Read on 2026-09-27, driver version
  10.1.1.38 is on the chipset's two USB 2.0 host controllers, *Intel(R) 6
  Series/C200 Series Chipset Family USB Enhanced Host Controller* `1C26` and
  `1C2D`, from `oem28.inf`. Whether the Linksys is attached behind either
  controller, and whether the install caused the loss, are not established.
- **The second address loss came eight hours after the install,** so the driver
  was already in place for it.
- **The stop that did not finish was the machine being switched off.** The
  service was told to stop in the same second the event log service stopped,
  and the machine went down before the service wrote its last line.
- **Neither the install nor the switch-off changed the adapter's index.** It was
  13 at 02:57:02, before both, and 13 at 03:46:47, after both.

Edwin's observation on 2026-09-27: since he installed the driver, the USB
adapter seemed to stop disconnecting, and it stayed up all day and night. What
was measured: from 19:07:35 on 2026-09-26 to 17:06:38 on 2026-09-27 the service
log has no `Could not reopen` line, so every reopen in that time found the
adapter usable. Whether the driver has anything to do with that is not
established; it was installed before the 11:35:33 loss.

## The sampler run

The sampler ran in its own ssh session on FIOS-STB-01. It wrote to a file in
the user's profile folder, outside the repository; below, that folder is shown
as `$HOME`. Its parsing was tested before the run in PowerShell against
made-up input.

```powershell
while ($true) { $i = $null; $c = @{}; foreach ($l in (netsh interface ipv4 show joins)) { if ($l -match '^Interface (\d+):') { $i = $matches[1] } elseif ($l -match '^\s*\d+\s+(\d+)\s+\S+\s+224\.0\.0\.251\s*$') { $c[$i] = $matches[1] } }; '{0}  13={1}  17={2}' -f (Get-Date).ToUniversalTime().ToString('u'), $c['13'], $c['17'] | Add-Content $HOME\sp-joins.txt; Start-Sleep -Seconds 5 }
```

Each line holds the time and the `References` count of `224.0.0.251` on index
13 and on index 17. An empty count means the interface was not listed. Two is
the baseline on each adapter: one membership from the service and one from a
process that has not been identified.

- **The test:** does a withdrawal follow the count on 13 falling below 2, and
  does the count return to 2 at the reopen, with no restart?
- **The control:** `Ethernet`, index 17, reads 2 throughout.
- **Void:** a restart; an empty `13=`, which a changed index would give; 17
  moving off 2; a gap in the samples.
- **How to read it:** below 2 before a withdrawal and 2 after the reopen is
  recovery from a real loss. Two throughout a withdrawal means the membership
  was not lost. A socket that had lost its membership would have been deaf
  since before the first unanswered reconfirmation, which falls at 80% of the
  record's lifetime, so a loss would show as a low count lasting at least a
  fifth of that lifetime. That is several samples if the lifetime is at least
  about 25 s. The printer's record lifetime was not read in this run.

The samples part of the check run at 16:30:54 on 2026-09-27 (its other part
listed the service log's withdrawal, recovery, reopen and start lines):

```powershell
$all = @(Get-Content $HOME\sp-joins.txt); 'samples: ' + $all.Count; 'first: ' + $all[0]; 'last:  ' + $all[-1]; $prev = $null; $gaps = @(foreach ($l in $all) { if ($l -notmatch '^\d{4}-\d\d-\d\d \d\d:\d\d:\d\dZ') { continue }; $t = [datetime]::ParseExact($l.Substring(0,20), "yyyy-MM-dd HH:mm:ss'Z'", [cultureinfo]::InvariantCulture); if ($prev -and ($t - $prev).TotalSeconds -gt 15) { '{0} -> {1}' -f $prev.ToString('u'), $t.ToString('u') }; $prev = $t }); 'gaps over 15 s: ' + $gaps.Count; $gaps | Select-Object -First 10; $bad = @($all | Where-Object { $_ -notmatch '13=2  17=2$' }); 'samples off 2: ' + $bad.Count; $bad | Select-Object -First 20
```

**Result:**

- 15,003 samples, from 19:07:35 on 2026-09-26 to 16:30:49 on 2026-09-27, about
  5.13 s apart on average. No gap over 15 s. None other than `13=2  17=2`.
- A later read at 17:06:38 found the 264 samples from 16:44:00 to 17:06:34 all
  `13=2  17=2`. The samples between 16:30:49 and 16:44:00 were not read.
- No restart, no empty count, and the control held, so the run is not void.

The withdrawals in the run:

| Printer unreachable | Reachable again | After | Retry question it fits |
|---|---|---|---|
| 06:10:21 | 06:10:44 | 23 s | 3, due at +22 s |
| 08:03:40 | 08:08:37 | 4 min 57 s | 8, due at +295 s |
| 11:48:30 | 16:58:09 | 5 h 9 min 39 s | 16, due at +18,575 s (16:58:05) |

The schedule is read from the code (`PrinterReachability.RecordSilence`): after
a withdrawal at T, the interval between unanswered questions starts at 1 s,
doubles, and is capped at 60 minutes, which first applies before question 13.
Each unanswered question also waits out the resolve timeout, 5 s by default; the
value configured on FIOS-STB-01 was not read. So question *n* falls at about
T + 5*n* + (2^*n* − 1) s up to question 12, and every 3,605 s after.

**What the run shows.** The count on 13 read 2 before, during and after every
one of the three withdrawals. None of them was a lost membership. By the code,
a reopen came before every question while the printer was held unreachable;
none failed and none was logged, as a routine reopen should not be. The
recoveries came through continuous querying (`REQ-RES-008`). Since no
membership was lost, they say nothing about what the reopen is for, which is
restoring one.

## The third withdrawal: the printer was silent on a working link

Read on 2026-09-27, with the service withdrawn since 11:48:30:

| Time | Read | Result |
|---|---|---|
| 16:35:53 | WLAN, adapter, address | `Wi-Fi` connected, signal 70%. Adapter `Up`, media connected, index 13. `192.168.12.136/24`, `Preferred`. |
| 16:39:11 | IPv4 interfaces and routes | 13 and 17 both `Connected`. On 13: `0.0.0.0/0` via `192.168.12.1`, `192.168.12.0/24` on-link, and its host, broadcast and multicast routes. On 17, the same set for its own network. All in the active store. |
| 16:42:01 | Both addresses | `Preferred`, `SkipAsSource` false, prefix and suffix origin `Dhcp`. |
| 16:44:34 | Pings sent from `192.168.12.136` (`ping -S`) | Gateway `192.168.12.1`: 3 of 3 answered, 2 ms; its neighbour entry on 13 went from `Stale` to `Reachable`. Printer `192.168.12.180`: three `Destination host unreachable`, reported by `192.168.12.136`; its neighbour entry on 13 was `Unreachable` before the pings and after them. |

By default Windows sends from an address only on the interface that holds it,
so these pings left on `Wi-Fi` whatever the routing choice described in the
next section. The gateway's answers are the control: the link was passing traffic.

Edwin then checked the printer. The printer said it was fine, and its own
connection check reported good. He switched it off and on, between 16:44:34 and
16:53:15.

| Time | Read | Result |
|---|---|---|
| before 16:53:15 | `Test-NetConnection 192.168.12.180 -Port 631` | Over `Wi-Fi`, from `192.168.12.136`: `TcpTestSucceeded : True`. |
| 16:53:15 | The same pings | Gateway 2 of 2. Printer 3 of 3 (113, 23 and 27 ms, TTL 64); its neighbour entry was `Reachable` before the pings. |
| 16:58:09 | Service log | `Printer reachable again: it answered on the printer-side interface.` |
| 16:58:11 | Service log | `Advertisement restored: the printer is on offer again.` |

What this establishes:

- **The 11:48:30 withdrawal was the printer going unheard while everything on
  this machine's side of the printer network worked:** the adapter, its
  address, its route, the gateway, and the service's membership.
- **At 16:44:34 nothing at the printer's address answered ARP** on that
  network, and the printer's own check, run after that and before the
  switch-off, reported good.
- **After the printer was switched off and on, the service recovered at its
  scheduled question,** 4 s after the time computed from the schedule, with no
  restart, no reopen line and no warning.

What it does not establish:

- **Why the printer stopped answering, or when.** The verdict at 11:48:30 means
  the printer's record ran out its lifetime unanswered. The printer last
  answered earlier than that, by up to that lifetime, which was not read.
- **What the printer's connection check tests.**
- **Whether switching the printer off and on is what brought it back,** or
  whether it would have come back by itself. Only that it answered afterwards.
- **Whether the two shorter withdrawals, at 06:10:21 and 08:03:40, had the same
  cause.** The membership held through both; nothing else was read at the
  time.
- **Whether the eleven withdrawals on 2026-09-26 had the same cause.**

## Windows chose the default route to the printer while it was silent

- Between 16:30:54 and 16:35:53 (no clock was taken),
  `Test-NetConnection 192.168.12.180 -Port 631` reported
  `InterfaceAlias : Ethernet`, `SourceAddress : 192.168.1.161`, the ping timed
  out, and `TcpTestSucceeded : False`.
- At 16:35:53 and 16:42:01, `Find-NetRoute -RemoteIPAddress 192.168.12.180`
  gave source `192.168.1.161` on `Ethernet` and route `0.0.0.0/0`, next hop
  `192.168.1.1`. At 16:39:11 the table held `192.168.12.0/24` on `Wi-Fi`, and
  `Wi-Fi`'s IPv4 interface was `Connected`.
- At 16:42:01, `netsh interface ipv4 show destinationcache` held an entry for
  `192.168.12.180` under `Interface 17: Ethernet`, next hop `192.168.1.1`, path
  MTU 1500, and none under `Wi-Fi`.
- After the printer was switched off and on, Edwin's `Test-NetConnection` went
  out on `Wi-Fi` from `192.168.12.136` and succeeded. At 16:53:15,
  `Find-NetRoute` gave `192.168.12.0/24` on `Wi-Fi` both before and after the
  pings, and the destination cache held `192.168.12.180` under
  `Interface 13: Wi-Fi` with the printer itself as next hop. The Ethernet entry
  was gone. Nothing had been cleared by hand.

What this adds to
[`2026-09-22-connections-to-the-printer-leave-by-the-default-route.md`](2026-09-22-connections-to-the-printer-leave-by-the-default-route.md):
that finding measured the default route being taken when the printer side had
no link. Here it was taken with the printer side up, holding its address and its
route to the printer's network, while only the printer was silent. The README's
point 5, "The connection to the printer follows the Windows routing table",
describes only the no-link case.

*(Note added 2026-09-27 by Claude (Anthropic model, Claude Opus 5.5) at the
direction of Edwin West. Reviewed by a human before merge. When this file was
first committed, Claude had read only point 5. The README's Section 3, under
step 5, also said that with the printer-side link up the way out is the printer
network, which the measurement above contradicts. Both places were corrected in
the commit that adds this note.)*

Not established:

- **What made Windows choose it.** Its choice may follow whether the printer
  answers ARP, or the cached path may have been left from an earlier moment and
  expired. The two changed together.
- **When the cached entry was made.** The `Test-NetConnection` at about 16:31
  would itself leave one like it.
- **Whether the service's own connection would have gone the same way.** It
  made none: while withdrawn, its listener is closed. `Test-NetConnection` stood
  in for it, as on 2026-09-22, and no packets were captured.

## Predictions

Before each read Claude stated what it expected.

Held: no start after 18:31:08; about 15,000 samples, no gap, the last within
seconds of the clock; the gateway answering; `SkipAsSource` false on both
addresses; `Printer reachable again` near 16:58:05 (it came at 16:58:09); the
02:57:02 start on `Ethernet`, index 17, with `Wi-Fi` usable at once.

Missed:

- The configuration lines of the 18:31:08 start were at 18:31:09.
- With `Find-NetRoute` choosing Ethernet, Claude predicted `Wi-Fi` would not
  show `192.168.12.136` as `Preferred` with the adapter `Up`. It did.
- Claude predicted no `192.168.12.0/24` route on index 13. There was one.
- After the printer was switched off and on, Claude predicted `Find-NetRoute`
  would still choose Ethernet. It chose `Wi-Fi`. Edwin's own
  `Test-NetConnection`, run just before, had already gone out on `Wi-Fi`.
- For 03:35 on 2026-09-26, Claude predicted a restart requested at about
  03:35:40. It was a power off, requested at 03:35:31.

## Mistakes

- **A wrong time for the service's question.** During the session Claude said
  the service's last question before 16:31 was "around 16:24". It had applied
  the 60-minute cap one question too early. The questions after 11:48:30 fell
  at about 15:58:00 and 16:58:05, and the answer logged at 16:58:09 fits the
  second.
- **A contradiction that was not one.** Claude said the service's view (the
  adapter usable at every reopen) and `Test-NetConnection`'s (the printer
  reached through Ethernet) disagreed unless the adapter had changed between
  them. The service's check, `StartupWait.Examine`, looks at the adapter's name,
  whether it is up and its address. It never looks at the routing table. Both
  were true at once.
- **Carried from 2026-09-26: a start went unread.** The configuration
  one-liner's `-Last 8` reached back only two of that day's three starts, so the
  adapter's index at 02:57:02 was not read until 2026-09-27.

## Side observation

`Wi-Fi` also carries a default route, through `192.168.12.1`, with interface
metric 55 against `Ethernet`'s 25. For the printer's address the more specific
`192.168.12.0/24` was chosen over either default route on 2026-09-22 with the
link up, and again at 16:53:15. At 16:35:53 and 16:42:01, it was not.
