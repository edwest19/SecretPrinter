# Ten days of port tests, and three Windows settings changed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-19. Reviewed by a human before merge.*

**Status: measured, once, by reading logs and settings on FIOS-STB-01; the
service and the printer were not touched. Afterwards Edwin West changed three
Windows settings on that machine, at his choice; they are recorded here
because later findings depend on them. Edwin's script `Check-SP.ps1` tested
the printer's port 1,444 times from 2026-10-09 to 2026-10-19. All 1,224 tests
that succeeded went through the printer-side adapter. Of the 220 that failed,
213 took Windows' default route out the client network, 212 of them while the
service itself was not offering the printer. That record replaces the seventh
and eighth occasions under the fifth point of README "Read this before
installing", which had been read from the script's log from 2026-10-15 only.
The script missed eight of its runs, all on 2026-10-14 and all within an hour
after a Windows restart; why is not established. The printer-side Wi-Fi
profile is set to connect automatically, and Microsoft documents a Windows
default that blocks new automatic connections to the internet while an
Ethernet one is up; whether that is why Windows never connects this adapter
by itself is not yet shown. No code changed.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name.

**About the times.** Times ending in `Z` are UTC, from the service's log, from
Windows' event logs or from the commands run on FIOS-STB-01. Times in am or pm
are US Eastern, UTC-4, as the script logs them. 2026-10-09 was a Friday,
2026-10-14 a Wednesday and 2026-10-19 a Monday.

## What was read, and what was changed

Read-only commands, run by Edwin in his SSH session on FIOS-STB-01 on
2026-10-19 between 3:50 and 6:27 pm, and pasted or attached:

1. The scheduled task that runs the script, as Task Scheduler holds it; its
   last and next run; whether Task Scheduler's history log was on; and the
   script's log around the restarts of 2026-10-14.
2. Every run the script logged since 2026-10-09, by day and by the gaps
   between runs; Windows' starts, stops and restart requests in that time;
   and the run of 5:32:15 am on 2026-10-14 in full.
3. The printer-side Wi-Fi profile's settings (not its key), the adapter's
   state, the Windows Connection Manager policy keys and Windows' view of each
   network.
4. How many Wi-Fi profiles are saved and how each connects (no names); the
   definition of the Connection Manager policy in Windows' own policy files;
   the local Group Policy file; the default routes and interface metrics.
5. Windows' version; every restart request and unexpected stop in the System
   log; Windows Update's installs and failures since 2026-10-01, its
   settings, policies and scheduled tasks, and whether a restart was pending;
   the definition of the active-hours policies.
6. Every port test in the script's log, with the adapter Windows reported
   and, from the service's own log, what the service held at that moment.

Changed by Edwin, with Windows' own tools (the Local Group Policy Editor,
reached by Remote Desktop, and `wevtutil`), each read back afterwards:

- **21:18:13Z:** "Minimize the number of simultaneous connections to the
  Internet or a Windows Domain" (Network, Windows Connection Manager) set to
  Enabled, "0 = Allow simultaneous connections".
- **21:48:37Z, then 22:01:02Z:** "Turn off auto-restart for updates during
  active hours" (Windows Components, Windows Update) set to Enabled, from
  9 AM to 3 AM. The hours first chosen were 6 AM to 12 AM; Edwin set the
  end at 3 AM, so it was first from 6 AM to 3 AM, 21 hours. Windows' own
  explanation of the policy gives 18 hours from the start as the longest span
  unless another policy says otherwise, and what it does with more is not
  documented in what was read, so the start was moved to 9 AM.
- **About 22:11Z:** Task Scheduler's operational log, its history, turned on
  (`wevtutil sl Microsoft-Windows-TaskScheduler/Operational /e:true`). It was
  off, as below.

None of these is the service's doing; the service changes no host
configuration (`REQ-SEC-007`).

## The script's runs, and the restarts

The task, as Task Scheduler holds it: a time trigger from 4:30:27 pm on
2026-10-09, repeated every 10 minutes with no end; run as `SYSTEM` at the
highest level; a new run not started while one is running; a time limit of 5
minutes; "start when available" off. That is what its registration script
asks for, with Windows' defaults for the rest.

The script logged 1,432 runs from its first, at 4:27:35 pm on 2026-10-09, to
3:50:27 pm on 2026-10-19. Every full day holds 144, except 2026-10-14, which
holds 136:

- **After the restart Windows Update began at 08:36:02Z** (the event log
  started again at 08:39:24Z), no run at 4:40, 4:50, 5:00, 5:10, 5:20 or
  5:40 am. The runs that came were late: 5:32:15, 5:52:16 and 6:04:02 am in
  place of 5:30, 5:50 and 6:00, then 6:10:35. From 6:20 am they were on the
  schedule again.
- **After the restart asked for at 20:44:32Z and the update's own at
  20:54:28Z** (the event log started at 20:50:17Z and at 20:56:03Z), no run at
  4:50 or 5:00 pm; the next was 5:10:29 pm.

Nowhere else in the ten days is a run missing. Three single runs began about a
minute late (4:11:28 pm on 2026-10-10, 9:01:38 pm on 2026-10-14 and 12:51:34
am on 2026-10-17), and on 2026-10-09 three runs fell off the schedule while
the task was being set up. No other restart of Windows happened in the ten
days.

**Why the runs after the restarts were skipped is not established.** Task
Scheduler's history log was off, so Windows kept no record of them. In the
run of 5:32:15 am the machine was slow: `Get-NetAdapter` took 36 seconds and
`netsh wlan connect` 23. The history is on now, so the next restart will
leave a record. Also not explained: Task Scheduler gave the last run as
19:50:50Z and the next as 20:00:00Z exactly, while the script logged that run
as beginning at 3:50:27 pm, and its runs begin 27 or 28 seconds past the tenth
minute.

**Two connections settled.** The [finding of 2026-10-14 on the
restart](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md) says
the printer-side Wi-Fi was connected at 09:33:11Z and 09:52:24Z that morning,
off the script's schedule, and that what made those connections was not
established. Both were the script's. Its run of 5:32:15 am found the Wi-Fi
disconnected and ran `netsh wlan connect` from 5:32:52 to 5:33:15 am (exit
code 0); Windows logged the connection starting at 09:33:10Z and succeeding at
09:33:11Z, while it ran. Its run of 5:52:16 am logged connecting at 5:52:23,
and Windows logged the connection at 09:52:24Z.

## The port tests, and the default route

The script tests TCP port 631 on the printer with `Test-NetConnection` at
every run. Its log holds 1,444 results, from 2026-10-09 to 6:20:27 pm on
2026-10-19. Three runs before 1:16 pm on 2026-10-15 logged none; which, was
not read.

For each test, what the service held was taken from the service's own log:
the last of its lines about the printer's state before the moment the test
began (the script's "Step 3" line, converted from Eastern time to UTC).
"Unreachable" means `Printer unreachable`, `No longer accepting print jobs`
or `Advertisement withdrawn`; "reachable" means `Printer reachable again`,
`Accepting print jobs` or `Advertisement restored`; "waiting" means a start
of the service, `Waiting for the printer-side interface` or `The printer did
not answer at startup`; otherwise "stopped". A change of state between that
line and the test would put a test in the wrong group; none was looked for.

| Result | Adapter Windows reported | The service held the printer | Tests |
| --- | --- | --- | --- |
| succeeded | printer-side | reachable | 1,180 |
| succeeded | printer-side | unreachable | 32 |
| succeeded | printer-side | (stopped) | 12 |
| failed | client-side (default route) | unreachable | 174 |
| failed | client-side (default route) | waiting at startup | 38 |
| failed | client-side (default route) | reachable | 1 |
| failed | printer-side | reachable | 4 |
| failed | printer-side | unreachable | 2 |
| failed | printer-side | (stopped) | 1 |

- **Every one of the 1,224 tests that succeeded went through the printer-side
  adapter.** The 32 while the service held the printer unreachable are the
  printer answering the script before the service found it again.
- **213 of the 220 that failed took the default route,** with the
  machine's client-side address as their source. 174 came while the service
  held the printer unreachable. 38 came from 5:32 am to 12:00 pm on
  2026-10-14, after the restart, while the service waited at startup for a
  printer that did not answer; it had logged `The printer did not answer at
  startup` at 09:33:22Z, and the printer answered again at 16:05:35Z, after
  Edwin restarted it (the [finding of that
  morning](2026-10-14-seventeen-answers-waited-at-random-on-a-network.md)).
  One came at 7:20 pm on 2026-10-10 while the service still held the printer
  reachable; at the next test, ten minutes later, it held it unreachable.
- **The 213 fall in 18 stretches of runs in a row.** 16 are before
  2026-10-15: the longest is 55 runs, from 6:50 pm on 2026-10-13 to 3:50 am
  on 2026-10-14, the next 36, from 6:10 am to 12:00 pm that day, and eight
  are single runs. The other two are the 61 runs of 2026-10-16 and the one run
  of 2026-10-17 recorded in the [finding of
  2026-10-18](2026-10-18-the-third-release-ran-three-days-on-fios-stb-01.md).
- **7 failed through the printer-side adapter:** at 4:50 pm on 2026-10-10,
  8:20 am on 2026-10-12, 6:10 am on 2026-10-16 and 1:50 am on 2026-10-19
  while the service held the printer reachable; at 2:00 am on 2026-10-13 and
  1:40 pm on 2026-10-19 while it held it unreachable; and at 1:20 pm on
  2026-10-10 while the service was stopped. Why the port did not answer those
  tests is not established.
- **Before 1:16 pm on 2026-10-15, at all 155 of the tests that failed,** the
  script had found the gateway of the printer's network answering its pings,
  and at all but four it had found the printer-side adapter up; at those four
  it had just connected it.

So Windows chose its default route for the printer's address at 213 of the
script's 220 failed tests and at none of its 1,224 successful ones, and at
212 of the 213 the service itself was not offering the printer. Why Windows
does that with the printer-side adapter up is still not established. These
were the script's tests, not the service's connections: the service accepts
none while the printer is withdrawn (`REQ-LIF-006`) and offers nothing while
it waits at startup (`REQ-LIF-008`).

**A count not reconciled.** The finding of 2026-10-18 says the script ran 447
times from 1:16 pm on 2026-10-15 to its reading at 19:27:20Z on 2026-10-18.
The counts by day read today give 445 runs begun in that time (64, 144, 144
and 93), all on the schedule. The 63 failures it counted agree with today's
reading. Which run count is right was not settled.

## The printer-side Wi-Fi, and Windows' rule on simultaneous connections

- **The profile** is the only Wi-Fi profile saved on the machine, applied to
  all users, set to "Connect automatically", to connect only if the network
  is broadcasting, and not to switch to other networks. The adapter was
  connected, by a connection made with that profile.
- **No Connection Manager policy was set.** The policy key and its
  `GroupPolicy` subkey held no values; a `Local` subkey held one value,
  `WCMPresent = 1`, which the Microsoft pages read do not describe. No local
  Group Policy file existed. Windows saw both networks as reaching the
  internet, and both as private.
- **Microsoft's page "Understanding and configuring Windows Connection
  Manager"** (Microsoft Learn, last updated 2025-07-08) says of the policy
  `fMinimizeConnections`: "The policy is on by default for Windows 8,
  Windows 8.1, and Windows 10." Of its setting 1, which it says "was the
  *Enabled* state for the policy in builds of Windows before Windows 10,
  version 1809, build 17763.404", it says: "any new automatic internet
  connection is blocked when the computer has at least one active internet
  connection to a preferred type of network", with Ethernet the most
  preferred, and "Users can still manually connect to any network." The page
  does not state a default for the newer form of the setting separately.
- **That fits what Windows' WLAN logs have shown on this machine:** no
  automatic connection of the printer-side adapter, only manual ones (the
  findings of [2026-09-20](2026-09-20-the-wlan-drops-and-nothing-retries.md),
  2026-10-14 and 2026-10-18), the script's `netsh wlan connect` among them. It
  is not shown to be the cause.
- **The test.** The definition in Windows' own `WCM.admx` names the key
  `Software\Policies\Microsoft\Windows\WcmSvc\GroupPolicy`, the value
  `fMinimizeConnections` and four choices, 0 to 3, as Microsoft's page
  describes. With the machine's only saved profile being the printer-side
  one, there is no other network Windows could join. Edwin set the policy to 0
  at 21:18:13Z. Read back at 21:31:38Z: the value 0; both default routes as
  before (route metric 0 for IPv4, 256 for IPv6), and the interface metrics
  unchanged, Ethernet 25 and the Wi-Fi 55, so Windows still prefers the
  client network for the internet. **By 22:26:32Z the Wi-Fi had not
  dropped**, so there is no result yet. The result will be in the WLAN log at
  the next drop the Wi-Fi has on its own: the log records how each connection
  was started, and the script's are "Manual connection with a profile". The
  script keeps running meanwhile.

## Windows Update's restarts

- **Windows 10 Pro 22H2, build 19045.7807.** The System log reaches back to
  2023-06-27, before the machine had its present name.
- **Since 2026-08-26, the first day the log names the machine FIOS-STB-01,
  Windows itself started these restarts:** at 21:43:51Z on 2026-08-26
  (`TrustedInstaller.exe`, five minutes after a restart asked for by
  `ChangePk.exe`, Windows' tool for changing the product key); at
  17:53:49Z on 2026-09-09, 1:53 pm (`TrustedInstaller.exe`, "Operating
  System: Upgrade (Planned)"), with no request from anyone in the minutes
  before and inside the default active hours, for reasons not established; at
  08:36:02Z on 2026-10-14, 4:36 am (`svchost.exe` for `SYSTEM`, "Operating
  System: Service pack (Planned)"), the morning after the month's update day;
  and at 20:54:28Z on 2026-10-14 (`TrustedInstaller.exe`), finishing that
  update after the restart asked for at 20:44:32Z. Every other restart or
  power-off since then was asked for on behalf of a person's account, except
  a power-off at 03:35:31Z on 2026-09-26 by `winlogon.exe` on behalf of
  `SYSTEM`, whose cause is not established. No unexpected stop (events 41
  and 6008) is logged after 2026-08-26.
- **Before the change,** the active hours were Windows' default, 8 AM to 5 PM;
  Windows had also stored a suggestion of 11 AM to 10 PM, and whether it used
  it is not shown. No Windows Update policy was set, no restart was pending,
  and Windows Update's restart tasks were disabled with no next run.
- **Microsoft's page "Manage device restarts after updates"** says:
  "Automatic restarts after an update occur outside of the active hours."
  Windows' explanation of the policy on this machine says the longest span is
  18 hours from the start unless another policy configures it, and none does.
- **With 9 AM to 3 AM,** Windows Update should restart the machine by itself
  only between 3 and 9 am. That has not yet been seen at a restart.

## What Claude predicted, and what it did not

Held: the boot time and no restart since 2026-10-14; the task's account,
action, trigger and settings, and the five lines of its registration script
that make them; the script's runs of 4:40 and 5:10 pm on 2026-10-14 and none
between; about 1,440 runs (1,432); the restarts of 2026-10-14 and no others
since 2026-10-09; the profile's interface lines; the policy's definition (key,
value, four choices); the editor being there; the routes and metrics, before
and after the change; no restart pending; the default active hours; both
changes read back as set; about 840 tests before 1:16 pm on 2026-10-15 (837)
and the three failures named in advance, all by the default route; about 1,445
tests in all (1,444); at least 218 failures (220); and the seventh failure
through the printer-side adapter falling after 2026-10-18's reading, while the
service held the printer unreachable.

Not predicted:

- **The task's "stop at end" setting is on;** Claude said off. It acts at the
  end of a repetition's duration, by Microsoft's description, and this task's
  has none. The trigger's start ends at 27 seconds; Claude said 28.
- **The last run's start:** Claude took it from Task Scheduler, 19:50:50Z; the
  script logged 3:50:27 pm.
- **The three single late runs.**
- **The Connection Manager policy key:** Claude said it most likely did not
  exist; it exists, empty but for `WCMPresent`. The profile also holds a
  second security entry, which was not predicted.
- **Unexpected stops before 2026-08-26:** Claude said probably none in the
  whole log; there are many, all before the machine had its present name.
- **38 failures while the service waited at startup;** Claude said a few. The
  failures through the printer-side adapter: Claude said five, all while the
  service held the printer reachable; there are seven, in three states. And
  the one failure by the default route while the service held the printer
  reachable.

## What this does not show

- **Whether Windows connects the printer-side Wi-Fi by itself** with the
  policy at 0: the Wi-Fi had not dropped by the last reading.
- **Why the script's runs were skipped after the restarts,** and what Task
  Scheduler will record at the next one.
- **The active hours at an update restart,** and what Windows would have done
  with a span of 21 hours.
- **Why Windows chooses the default route** with the printer-side adapter up,
  or what the service's own connections did at those times.
- **Why seven tests failed through the printer-side adapter,** four of them
  while the service held the printer reachable.
- **What `WCMPresent` does,** and why the restart of 2026-09-09 came inside
  the active hours.

## How it was read

The commands above, given to Edwin and run by him in an SSH session on
FIOS-STB-01, each read-only; the three changes were made by him in the Local
Group Policy Editor over Remote Desktop and with `wevtutil`, and read back by
read-only commands. The commands masked link-local and other IPv6 addresses,
IPv4 link-local addresses, hardware addresses, ids, the printer's host name
and the printer-side network's name, the last read from the script's own
setting; the profile's key was not asked for. The machine's addresses on both
networks, and the printer's, are in what was pasted and are not repeated
here; so are the machine's earlier names, which the System log holds. One
attachment carried, at the ends of some wrapped rows, leftover characters
from the window that were not output; they were ignored. The counts by day,
the stretches and the comparison with the finding of 2026-10-18 were worked
out in Claude's workspace from the pasted lines.
