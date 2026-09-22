# A refused start is reported to Windows as a timeout

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

**Status: measured from the records on FIOS-STB-01, NOT fixed. Nine times
between 2026-09-18 and 2026-09-19 the service refused its configuration under
the Windows service control manager. Each time it wrote the reason to its log
at once, and each time Windows recorded something else: "did not respond to the
start or control request in a timely fashion" (error 1053) and a connection
timeout. Windows never learns that the service refused, or why. Every one of
the nine refusals was the printer-side adapter not being usable at startup.**

## What the code does

In `src/SecretPrinter.Service/Program.cs` at `ab27655`:

- `RunAsync` loads the configuration first. A `ConfigurationException` is
  logged and the method returns 3 (line 157).
- Only after that, and only with `--service`, does `RunAsWindowsService` call
  `System.ServiceProcess.ServiceBase.Run` (line 208).

So under `--service` a refused configuration ends the process before it has
connected to the service control dispatcher. The control manager started a
service process that never checked in with it; from its side, that is a
service that did not respond. Exit code 3, and the usage text that documents
it ("Configuration is not usable; every problem is listed"), mean something
only to a console run.

An adapter that is down is a configuration refusal, not a startup failure.
`ConfigurationLoader` resolves the printer interface by name and turns the
resolver's `MdnsInterfaceException` into a problem line
(`printerInterface: ...`), so it takes the exit-3 path above rather than the
exit-4 path in `ServiceHost`.

## What the records show

From the service log (`C:\ProgramData\SecretPrinter\secretprinter.log`, by
`Select-String -Pattern "Configuration is not usable" -Context 0,3`) and the
System event log (`Service Control Manager`, IDs 7000 and 7009, times converted
to UTC). All commands were run by Edwin on FIOS-STB-01; the outputs are his
pastes.

| Refusal logged (UTC) | Problem line in the service log | Event 7000 | Event 7009 |
|---|---|---|---|
| 2026-09-18 21:46:40 | `Interface 'Wi-Fi' holds 169.254.111.167 but is not up.` | 21:46:40, did not respond in a timely fashion | 21:46:40, 30000 ms |
| 2026-09-18 21:47:21 | `Interface 'Wi-Fi' holds 169.254.111.167 but is not up.` | 21:47:21, same | 21:47:21, 30000 ms |
| 2026-09-18 22:45:31 | `Interface 'Wi-Fi' holds 169.254.111.167 but is not up.` | 22:45:31, same | 22:45:31, **45000 ms** |
| 2026-09-19 05:23:07 | `Interface 'Wi-Fi' has no IPv4 address, so it cannot be used.` | 05:23:07, same | 05:23:07, 30000 ms |
| 2026-09-19 05:24:12 | `Interface 'Wi-Fi' has no IPv4 address, so it cannot be used.` | 05:24:12, same | 05:24:12, 30000 ms |
| 2026-09-19 05:24:48 | `Interface 'Wi-Fi' has no IPv4 address, so it cannot be used.` | 05:24:48, same | 05:24:48, 30000 ms |
| 2026-09-19 05:26:23 | `Interface 'Wi-Fi' has no IPv4 address, so it cannot be used.` | 05:26:23, same | 05:26:23, 30000 ms |
| 2026-09-19 05:35:46 | `Interface 'Wi-Fi 2' holds 192.168.12.136 but is not up.` | 05:35:46, same | 05:35:46, 30000 ms |
| 2026-09-19 18:23:11 | `Interface 'Wi-Fi 2' holds 192.168.12.136 but is not up.` | 18:23:11, same | 18:23:11, 30000 ms |

Every refusal in the log has a matching pair of events, so all nine were
starts by the control manager; none was a console run. Each problem list had
exactly one line. The adapter named changes from `Wi-Fi` to `Wi-Fi 2` between
05:26 and 05:35 on 2026-09-19, which is the configuration being edited, not
something this finding examined.

The log line reading "starting under the service control manager" is written
from `OnStart`, which is reached only after `ServiceBase.Run`. None of the nine
refusals has one, as the code predicts.

## Why it matters more than the wording

The service is installed `start= auto`, and startup refuses a printer-side
adapter that is not up. `REQ-LIF-006` covers the printer going away after
startup; nothing covers it being away at startup. So a reboot during a WLAN
outage should leave the service stopped rather than withdrawn and waiting, and
the only thing Windows would say about it is that the service hung. That
consequence is reasoned from the code and the install command. It was not
observed at a boot: whether any of the nine refusals above happened during a
boot was not checked.

`REQ-LIF-004` asks for "a fast, loud failure". The failure is fast, and it is
loud in the service's own log. It is not loud where an operator looks first,
the Services console and the System event log, which report a timeout instead.
The `REQ-LIF-004` markers sit on `ServiceHost.RunAsync` and `MdnsSocket`, not on
the configuration path, so no marker in the repository claims this path is
compliant. The gap is in the requirement's reach, not in a false marker.

## What is not established

- **The timing.** Claude predicted each event pair would follow its refusal by
  about 30 seconds. They carry the same second as the refusal. The earlier
  finding [`2026-09-18-the-printer-side-interface-goes-away.md`](2026-09-18-the-printer-side-interface-goes-away.md)
  said the control manager "waits its full 30 seconds"; the timestamps do not
  show that on their face. When the start request was issued was not recorded,
  so how long the control manager waited from its side is not known, and why
  it logged a timeout in the same second the process refused is not explained
  here.
- **The 45000 ms timeout** at 22:45:31 on 2026-09-18. The other eight say
  30000 ms. Not examined.
- **What `sc.exe query` reports as the exit code** after such a refusal. Not
  measured.
- **Whether any refusal was at boot**, as above.

## Seen in the same output, not part of this finding

The same event query returned three event 7000 entries on 2026-09-18 at
19:29:08, 19:30:03 and 19:31:49 reading "Access is denied." with no 7009 and
no matching line in the service log. `docs/operating.md` describes installs
that fail with error 5, *Access is denied*, but whether these three were one of
those was not checked.

Also noticed while reading the code, reasoned and not observed: when the
configuration is accepted but `ServiceHost.RunAsync` then fails (the exit-4
class of failure), under `--service` that happens after `OnStart` has returned.
`ServiceLifecycle` records it in `Failure` and logs "Service stopped because of
an error", but nothing in `WindowsService` asks the control manager to stop,
and `Failure` is read only in `OnStop`. Whether the service is then left
showing as running with nothing running has not been checked.

## What is owed

1. **A requirement.** Proposed wording, not yet in the README: *Under the
   Windows service control manager, a refusal to start is reported to the
   control manager as a failure to start, not left to time out, and the reason
   is in the log.* The ID is assigned when it is added.
2. **The fix, in its own commit.** Under `--service`, the process has to reach
   `ServiceBase.Run` before it can report anything to the control manager, so
   the refusal must be reported from inside the service rather than before it.
   The exact mechanism is to be chosen against the `ServiceBase` documentation
   and verified on FIOS-STB-01 by starting the service with its printer-side
   adapter down, reading the same two logs as above.
3. **Whether the service should start at all while the printer side is down**,
   or start withdrawn as `REQ-LIF-006` behaves after startup. That changes what
   startup refuses, which is a design decision for Edwin, separate from how a
   refusal is reported.
