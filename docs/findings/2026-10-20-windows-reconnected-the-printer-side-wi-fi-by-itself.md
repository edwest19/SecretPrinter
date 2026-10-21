# Windows reconnected the printer-side Wi-Fi by itself

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-20. Reviewed by a human before merge.*

**Status: measured, once, by reading logs on FIOS-STB-01 on 2026-10-20; the
service and the printer were not touched. On 2026-10-19 Edwin West set the
Windows policy "Minimize the number of simultaneous connections to the
Internet or a Windows Domain" to 0, "Allow simultaneous connections" (the
[finding of 2026-10-19](2026-10-19-ten-days-of-port-tests-and-three-windows-settings-on-fios-stb-01.md)).
In the 20 hours after, the printer-side Wi-Fi was disconnected twice by its
driver, and both times Windows connected it again by itself, about two
seconds later. Before the change, in every WLAN log read on this machine,
Windows never connected that adapter by itself. Two drops, one machine.
Edwin keeps the policy at 0. No code changed.**

*(Status note, 2026-10-21, by Claude, Claude Opus 5.5: the same logs were read
again on FIOS-STB-01 on 2026-10-21, read-only, for the day from 20:30:45Z on
2026-10-20 to 20:17:02Z, with the policy still at 0 and no boot since
2026-10-14. The driver disconnected the Wi-Fi twice more, at 23:51:56Z on
2026-10-20 and at 15:48:13Z on 2026-10-21, and both times Windows connected it
again by itself, 2.2 seconds later ("Automatic connection with a profile"):
four drops since the change, and Windows reconnected all four. The service
logged `The printer-side interface is usable again:` at 23:52:03Z, after the
first of the two, and nothing after the second; a drop shorter than its
5-second examination may not be seen. Twice the script found the gateway not
answering a ping while the Wi-Fi was connected, at its runs begun at 00:00:27Z
and 08:20:28Z on 2026-10-21, and disconnected and connected the Wi-Fi itself
(an `8003` with "The network is disconnected by the user", then a manual
connection). Windows did not disconnect or reconnect the link before the script
did: the first time, the WLAN log holds no disconnect between Windows'
reconnection at 23:51:58Z and the script's disconnection, and the second time
none between the script's run of 08:10:28Z, which found the gateway answering,
and its disconnection. The second time, the service had lost the printer at
08:15:49Z and found it at 08:20:55Z, 2 seconds after the script's connection.
The first time, the printer itself had not answered on its port since the
script's run of 23:10:27Z, and did not until Edwin restarted it ([the release
finding's status
note](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md)).
That day the log also holds 11 lone `11004` events, none within 15 seconds of a
disconnect or an `11006`: four came 50 to 114 seconds before four of the
service's six losses began, one came during a loss, and six had no loss begin
in the 12 minutes after. What they are is still not shown.)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name.

**About the times.** Times ending in `Z` are UTC, from Windows' event logs and
the service's log as the command printed them. Times in am or pm are US
Eastern, UTC-4.

## What was set, and what was read

On 2026-10-19 at 21:18:13Z Edwin set the policy with the Local Group Policy
Editor (Computer Configuration, Administrative Templates, Network, Windows
Connection Manager) to Enabled, "0 = Allow simultaneous connections". Read
back at 21:31:38Z, the default routes and the interface metrics were as
before: Windows still prefers the client network for the internet. The
machine's only saved Wi-Fi profile is the printer-side one, set to connect
automatically. Edwin's script `Check-SP.ps1` went on running every ten
minutes, as before; it connects the Wi-Fi with `netsh wlan connect` when it
finds it down.

On 2026-10-20 Edwin ran one read-only command in his SSH session on
FIOS-STB-01, which printed at 17:14:14Z:

- the policy's value: 0;
- the last boot: 2026-10-14 20:55:31Z, so no restart since the change;
- every WLAN-AutoConfig event since the change with ids 4003, 8000, 8001,
  8002, 8003, 11004 and 11006, with each event's first line and its
  `Connection Mode` and `Reason` lines;
- the script's lines since the change saying it found the Wi-Fi down or the
  gateway silent, and was connecting;
- the service's lines since the change about losing and finding the printer,
  its socket, errors, starts and stops.

## The two drops

| Time (UTC) | Event | What it says (each message's first line shortened; the `Connection Mode` and `Reason` as printed) |
| --- | --- | --- |
| 05:31:45.705 | 11004 | Wireless security stopped. |
| 05:31:50.722 | 11006 | Wireless security failed. Reason: Dynamic key exchange did not succeed within configured time |
| 05:31:51.309 | 11004 | Wireless security stopped. |
| **05:31:51.310** | **8003** | **disconnected. Connection Mode: Manual connection with a profile. Reason: The network is disconnected by the driver.** |
| 05:31:53.232 | 8000 | started a connection. **Connection Mode: Automatic connection with a profile** |
| **05:31:53.336** | **8001** | **connected. Connection Mode: Automatic connection with a profile** |
| 14:31:43.844 | 11004 | Wireless security stopped. |
| 14:31:47.888 | 11004 | Wireless security stopped. |
| 14:31:52.901 | 11006 | Wireless security failed. Reason: Dynamic key exchange did not succeed within configured time |
| 14:31:53.436 | 11004 | Wireless security stopped. |
| **14:31:53.436** | **8003** | **disconnected. Connection Mode: Automatic connection with a profile. Reason: The network is disconnected by the driver.** |
| 14:31:55.653 | 8000 | started a connection. **Connection Mode: Automatic connection with a profile** |
| **14:31:55.775** | **8001** | **connected. Connection Mode: Automatic connection with a profile** |

- **The first drop, at 1:31 am:** disconnected by the driver at 05:31:51.310Z
  and connected again at 05:31:53.336Z, 2.0 seconds later, by a connection
  Windows started itself.
- **The second, at 10:31 am:** disconnected at 14:31:53.436Z and connected
  again at 14:31:55.775Z, 2.3 seconds later, the same way.
- **The connection mode of each `8003` is that of the connection it ended**
  (as `docs/operating.md` says). The first drop ended a manual connection,
  made before the change; the second ended the one Windows had made at the
  first drop ("Automatic"), which had lasted nine hours.
- **The script connected nothing.** It logged no line saying it found the
  Wi-Fi down, or the gateway silent, after the change. Its runs come every ten
  minutes; both drops were over in seconds.

The command printed six more `11004` lines, "Wireless security stopped.", with
no disconnect or connection after any of them (05:10:28Z, 07:00:50Z,
12:55:50Z, 13:01:45Z, 14:18:06Z and 14:52:52Z). It did not print the events
`11010` and `11005`, so what followed each of these six in the log is not
shown.

## Before the change

In every WLAN-AutoConfig log read on this machine before 2026-10-19, Windows
did not connect the printer-side adapter by itself: after a drop the driver
initiated, the log recorded no attempt until a person or the script made one
(the findings of
[2026-09-20](2026-09-20-the-wlan-drops-and-nothing-retries.md),
[2026-10-11](2026-10-11-the-start-over-ran-overnight-on-fios-stb-01.md),
[2026-10-14](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md)
and [2026-10-18](2026-10-18-the-third-release-ran-three-days-on-fios-stb-01.md),
and the [finding of 2026-10-19](2026-10-19-ten-days-of-port-tests-and-three-windows-settings-on-fios-stb-01.md),
which lists the connections those logs hold as manual ones). Overnight on
2026-10-10 and 2026-10-11 the Wi-Fi stayed down for 3 min 37 s to 7 min 44 s
each time, until the script connected it. On 2026-09-21 it stayed down for
13 h 29 min, until a person did.

The policy is the only setting of Windows' networking changed between those
readings and this one. The two other changes of 2026-10-19 were to the hours
in which Windows Update restarts the machine and to Task Scheduler's history.

## What Microsoft says the policy does

Microsoft's page "Understand and Configure Windows Connection Manager"
(Microsoft Learn, last updated 2025-07-08, read again 2026-10-20):

> This policy is configured by using the **fMinimizeConnections** Group
> Policy. The policy is on by default for Windows 8, Windows 8.1, and Windows
> 10.

> If the policy setting is **1**, any new automatic internet connection is
> blocked when the computer has at least one active internet connection to a
> preferred type of network. [...] Ethernet is always preferred when
> connected. Users can still manually connect to any network.

Microsoft's page "Policy CSP - ADMX_WCM" (Microsoft Learn, read 2026-10-20), on
the same policy:

> If this policy setting is set to 0, a computer can have simultaneous
> connections to the internet, to a Windows domain, or to both. Internet
> traffic can be routed over any connection - including a cellular connection
> and any metered network.

The page does not say which setting the default is in Windows 10 version
1809 and later. Both of this machine's networks reach the internet, and
Windows counts both as doing so (read 2026-10-19). What was seen fits setting
1: with the policy not set, Windows started no connection to the printer-side
network while the Ethernet was up, and connections made by a person or the
script went through; at 0, Windows started one, twice.

## A security guideline that says the opposite

DISA's Security Technical Implementation Guide for Windows 10, rule
WN10-CC-000055 (as shown by stigaview.com for version 3, release 6, read
2026-10-20), is titled "Simultaneous connections to the internet or a Windows
domain must be limited." It says: "Multiple network connections can provide
additional attack vectors to a system and must be limited." A value of 0 is a
finding under it; it asks for the value 3, "Prevent Wi-Fi When on Ethernet", or
for the policy not to be set. That guideline is written for machines meant to
be on one network. A machine running SecretPrinter is on two by design, and
with 3 Windows would not let its Wi-Fi connect while its Ethernet is up, so
SecretPrinter could not work over a Wi-Fi printer side at all. At the default,
a dropped printer-side Wi-Fi stays down until someone connects it again.
Whether to set 0 is the owner's decision for their own machine. The service
does not set it, and will not (`REQ-SEC-007`).

## Edwin's decision

Asked on 2026-10-20, with what the two pages and the guideline say, he chose
to keep the policy at 0. The script keeps running, so a drop Windows
does not reconnect is still connected at the script's next run, within about
ten minutes.

## The service in that time

The service's log since the change holds 11 losses of the printer, each with
`Printer unreachable` and `Advertisement withdrawn`, and each ended with
`Printer reachable again` and `Advertisement restored`:

| Lost (UTC) | Found again | Restored |
| --- | --- | --- |
| 00:20:22 | 00:20:59 | 00:21:02 |
| 01:26:27 | 01:29:11 | 01:29:14 |
| 03:26:22 | 03:29:06 | 03:29:09 |
| 04:47:28 | 04:47:42 | 04:47:45 |
| **05:33:35** | **05:36:19** | **05:36:22** |
| 07:10:42 | 07:10:42 | 07:10:45 |
| 08:40:20 | 08:40:56 | 08:40:59 |
| 13:02:45 | 13:02:51 | 13:02:54 |
| 13:29:22 | 13:29:58 | 13:30:01 |
| 13:48:12 | 13:49:09 | 13:49:12 |
| 15:36:33 | 15:39:17 | 15:39:20 |

- **One loss began 1 min 42 s after the first drop's reconnect** (05:33:35Z,
  shown in bold), and the printer was offered again 2 min 47 s after that.
  Whether it had anything to do with the drop is not established: the printer
  goes quiet by itself several times a day, and the WLAN log holds no
  disconnect near the other ten.
- **None followed the second drop.** The loss before it ended at 13:49:12Z,
  and the next began at 15:36:33Z.
- **No line about the printer-side socket or the adapter** (`socket reopened`,
  `Could not reopen`, `usable again`), no error, and no start or stop. The
  service examines the adapter every 5 seconds (`REQ-RES-010`), and
  `docs/operating.md` already says a drop shorter than that may not be seen;
  whether either drop was seen is not shown.

## What Claude predicted, and what it did not

Held: the time of the reading; the policy at 0; the boot of 2026-10-14 and no
restart since; no line from the script; several losses and recoveries of the
printer in the service's log (11), with no start or stop. The WLAN events were
the test, and were not predicted. Claude said lone `11004` lines were probably
the link being re-authenticated; this reading cannot show whether they were.

## What this does not show

- **More than two drops.** Both were reconnected by Windows within 2.3
  seconds; a drop Windows does not reconnect has not been seen since the
  change, and the script would still connect it.
  *(Status note, 2026-10-21, by Claude, Claude Opus 5.5: four by then, all
  reconnected by Windows; see the status note at the top.)*
- **After a boot.** The [finding of 2026-10-14 on the
  restart](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md)
  records that Windows did not connect the Wi-Fi after a boot. No boot has
  happened since the change.
- **Why the link drops.** Both drops were ended by the driver after a failed
  key exchange. Microsoft's page also describes Windows "soft-disconnecting"
  networks under the policy; both disconnects read here gave the driver as
  their reason, and nothing here connects the drops to the policy.
- **The routes now.** They were read after the change on 2026-10-19 and not
  again.
- **What the six lone `11004` events were,** and whether the loss at
  05:33:35Z had anything to do with the first drop.
  *(Status note, 2026-10-21, by Claude, Claude Opus 5.5: still not shown;
  eleven more the next day, four of them shortly before a loss; see the status
  note at the top.)*

## How it was read

The command, given to Edwin and run by him in an SSH session on FIOS-STB-01,
read-only. It masked hardware addresses, ids, link-local and other IPv6
addresses, IPv4 link-local addresses, the printer's host name and the
printer-side network's name, the last read from the script's own setting. The
reading came as an attachment. The durations were worked out in Claude's
workspace from the printed times.
