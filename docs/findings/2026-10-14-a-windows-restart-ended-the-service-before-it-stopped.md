# A Windows restart ended the service before it stopped

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-14. Reviewed by a human before merge.*

**Status: corrected in the change this finding comes with, and measured against
the code by three tests, one of them run on Windows; not yet run at a real
restart. At every shutdown or restart of Windows since the first commit, the
service's stop never began. By the code, and by the exception measured on
Windows: Windows handed the service its shutdown; the service logged that it
was stopping, asked Windows for more time, and that request threw an exception
which ended the process. No goodbye records were
sent, nothing was closed in order, and no summary was written. Releases 0.1.0
and 0.2.0 do this. It was seen on FIOS-STB-01 on 2026-10-14, when Windows
restarted it at 08:38Z while nothing was offered, so nothing lingered that
time. It is stated as README `REQ-LIF-009` and under "Known problems".**

*(Status note, 2026-10-14, by Claude, Claude Opus 5.5: run at a real restart
later the same day, on FIOS-STB-01, in a build of `8663e5c` made there and
installed as the service, with the printer offered. Before the boot the
service's log holds every line that "What a run at a real restart would need
to show" names, all at 20:49:12Z, and the System log holds no application
error from `SecretPrinter.Service.exe`. Windows then restarted again by itself
to finish installing an update, while the service waited at startup, and that
stop ended as the first test below expects. The goodbye was logged and not
captured on the wire. See [the finding of the
run](2026-10-14-the-service-stopped-cleanly-at-a-windows-restart.md).)*

No IPv6 address, hardware address or device name appears in this finding, and
no machine's address on either network.

**About the times.** Times ending in `Z` are UTC, from FIOS-STB-01's service
log and from Windows' event logs as Edwin West read them with commands given to
him. Eastern times are given where the commands used them.

## What was seen

On 2026-10-14 FIOS-STB-01 ran release 0.2.0 as its service. The printer had
been withdrawn since 22:43:24Z the evening before; the service was running and
asking for the printer on its schedule.

**Windows' System log,** from 04:35 Eastern to the boot:

| Time (UTC) | Event | Source | What it says |
|---|---|---|---|
| 08:36:02.111 | 1074 | User32 | `svchost.exe` has initiated the restart of the computer on behalf of `NT AUTHORITY\SYSTEM`, for a reason beginning "Operating System: Service pa" (the command cut the text there) |
| 08:36:02.242 | 20 | WindowsUpdateClient | an update failed to install, error 0x8024001E |
| 08:38:15.479 | 7043 | Service Control Manager | the Delivery Optimization service did not shut down properly after a preshutdown control |
| 08:38:15.720 | 7034 | Service Control Manager | the OpenSSH SSH Server service terminated unexpectedly |
| 08:38:17.468 to .475 | 50104 and others | DHCP clients | the DHCPv4 and DHCPv6 clients stop |
| 08:38:18.570 | 6006 | EventLog | the event log service was stopped |
| 08:38:20.021 | 4001 | WLAN-AutoConfig | the WLAN AutoConfig service has stopped |
| 08:38:22.622 | 109 | Kernel-Power | the kernel power manager has initiated a shutdown transition |
| **08:38:29.185** | **26** | **Application Popup** | **`SecretPrinter.Service.exe - Application Error : The exception unknown software exception (0xe0434352)`** |
| 08:38:34.066 | 13 | Kernel-General | the operating system is shutting down |

Windows gave its boot time as 08:38:56Z. 0xe0434352 is the exception code of a
.NET exception: Microsoft's page "CLR Exception E0434352"
(https://learn.microsoft.com/en-us/shows/inside/e0434352, read 2026-10-14) says
"A CLR Exception is a type of Exception made by .NET applications" and "The
Exception Code is 0xE0434352".

**The service's log** holds, between its last line before the restart and its
first line after it:

```
2026-10-13 22:50:31Z  INFORMATION  Printer-side socket reopened on Wi-Fi (...), and 224.0.0.251 joined again.
2026-10-14 08:38:17Z  INFORMATION  Service 'SecretPrinter' stopping.
2026-10-14 08:39:44Z  INFORMATION  Service 'SecretPrinter' starting under the service control manager.
```

Nothing after `stopping`: no goodbye line, no summary, no `SecretPrinter
stopped.`, and no `Shutdown did not finish within 20s`, which the service writes
when its own stop takes longer than that (`ServiceLifecycle`). The log file is
flushed line by line (`FileServiceLog` sets `AutoFlush`), so each line is handed
to Windows as it is written: the missing lines were most likely never written,
rather than lost on the way to the disk.

**Windows' Application log,** 04:30 to 04:39:50 Eastern, holds no entry from
SecretPrinter at all: neither a failure at 08:38 nor the "Service started
successfully." that .NET's `ServiceBase` writes there by default after a start
(its `AutoLog` is on unless turned off, and this service does not turn it off).
Other services' entries of that kind are there. Why SecretPrinter's are not was
not established.

**On FIOS-STB-01,** `WaitToKillServiceTimeout` under
`HKLM\SYSTEM\CurrentControlSet\Control` reads 5000, and `sc.exe query
SecretPrinter` shows `STOPPABLE, NOT_PAUSABLE, ACCEPTS_SHUTDOWN`.

## Why

**The service's code, since the first commit (`699598b`, 2026-09-05):**
`WindowsService.OnShutdown` called `OnStop`. `OnStop` logged `stopping`, then
asked Windows for 30 seconds more with `RequestAdditionalTime`, and only then
stopped the work.

**.NET's `ServiceBase`,** read in `ServiceBase.cs` of
System.ServiceProcess.ServiceController, the same file at tags v10.0.0, v10.0.11
and v10.0.12 of dotnet/runtime (the service uses package 10.0.11):

- For a stop request, the control handler marks the service `STOP_PENDING`,
  tells the control manager, and queues `DeferredStop`, which marks it
  stop-pending again and calls `OnStop`.
- For a shutdown, the control handler queues `DeferredShutdown` with the
  service's state left as it was, running, and `DeferredShutdown` calls
  `OnShutdown`.
- `RequestAdditionalTime` throws `InvalidOperationException` unless the state is
  one of the four pending states. Its message, from the same tags'
  `Strings.resx`: "UpdatePendingStatus can only be called during the handling
  of Start, Stop, Pause and Continue commands."
- `DeferredShutdown` catches what `OnShutdown` throws, tries to write it to the
  event log, and throws it again. It runs as a work item queued to the thread
  pool, where nothing catches it. Microsoft's page "Exceptions in managed
  threads" (https://learn.microsoft.com/en-us/dotnet/standard/threading/exceptions-in-managed-threads,
  updated 2026-03-19, read 2026-10-14) says: "The common language runtime
  allows most unhandled exceptions in threads to proceed naturally. In most
  cases, this means that the unhandled exception causes the application to
  terminate."

So at a shutdown `OnShutdown` called `OnStop`, `OnStop` logged `stopping` and
asked for more time, the request threw, and the process ended before
`ServiceLifecycle.Stop` was called. That fits every line the service wrote,
every line it did not, and the application error Windows recorded; the twelve
seconds before the error are not accounted for (below).

**Measured on Windows,** on the development machine before the correction, by
the first of the tests below: calling `OnShutdown` the way `ServiceBase` does
threw `System.InvalidOperationException: UpdatePendingStatus can only be called
during the handling of Start, Stop, Pause and Continue commands.`

## Not established

- **Why 12 seconds passed** between `stopping` at 08:38:17Z and the application
  error at 08:38:29Z. `ServiceBase` tries to write the failure to the event log
  before throwing it again, and the event log service stopped at 08:38:18.570Z;
  whether that took the time was not measured.
- **Why the Application log holds no entry from SecretPrinter.**
- **Whether earlier restarts did the same.** The findings record no earlier
  restart of Windows with the service running: the start at boot of 2026-10-06
  followed a `Stop-Service`
  ([finding](2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
  FIOS-STB-01 also booted at 00:29:01Z on 2026-10-08, when it ran the service as
  a build from source; its log for that restart has not been read. By the code,
  every such restart did the same.
- **What Windows did with the process between the error and the shutdown,**
  and whether the 5 seconds of `WaitToKillServiceTimeout` came into it.

## What was at stake

- **The goodbye.** README `REQ-LIF-003` says the service sends goodbye records
  on shutdown, and the code's marker on `OnShutdown` said "A machine shutdown
  retracts the advertisement the same way a service stop does, so the printer
  does not linger in client caches after a reboot". At a shutdown of Windows
  that was never so. A client could go on listing the printer after the machine
  went down, until the records expired: 120 seconds for the address and SRV
  records, 4,500 seconds, 75 minutes, for the browse and TXT records (as the
  capture of the same day read them). On 2026-10-14 nothing was offered, so
  nothing was retracted or left behind.
- **`REQ-LIF-001`, "starts and stops cleanly", and `REQ-LIF-002`, leaving groups
  and closing sockets on shutdown.** Neither held at a shutdown of Windows.
  Windows releases a process's sockets when the process ends.
- **A stop request was not affected.** `ServiceBase` marks the service
  stop-pending before `OnStop`, so the request for more time is allowed there.
  The findings record stop requests finishing with their summary, as on
  FIOS-STB-01 at 16:40:48Z the same day, when the stop logged everything from
  `stopping` to `SecretPrinter stopped.` within one second.

## The correction

In `src/SecretPrinter.Service/WindowsService.cs`:

- **`OnShutdown` no longer calls `OnStop`.** It logs `Service 'SecretPrinter'
  stopping: Windows is shutting down.` and calls `StopTheWork`.
- **`OnStop` logs `stopping`, asks for more time as before, and calls
  `StopTheWork`.** Asking is allowed there, because `ServiceBase` marks the
  service stop-pending before calling `OnStop`.
- **`StopTheWork`** is what `OnStop` did after asking for time: it calls
  `ServiceLifecycle.Stop`, which cancels the work and waits for it, warns if it
  did not finish, and reports a failure from before the stop.
- **Nothing asks for more time at a shutdown.** Windows waits for a service as
  long as `WaitToKillServiceTimeout` says on that machine, 5 seconds on
  FIOS-STB-01. A stop request there took under a second on 2026-10-14.
- The header gains a section, "When Windows shuts down", with the reading of
  `ServiceBase` above. A comment in the constructor that said the control
  manager does not send shutdown requests the service would do anything
  different with, beside `CanShutdown = true`, is corrected.
- `REQ-LIF-009` markers on `OnShutdown` and `OnStop`; the `REQ-LIF-003` marker
  stays on `OnShutdown`, whose claim is now what the code does.

## The tests

In the new `tests/SecretPrinter.Service.Tests/ShutdownTests.cs`:

- **A Windows shutdown stops the work, without asking Windows for more time.**
  It builds the real `WindowsService` with a configuration whose printer-side
  adapter no machine has, starts it so that its work waits at startup, and
  calls `OnShutdown` by reflection, as `ServiceBase` does: with the service not
  marked stop-pending. A service built outside the control manager is in no
  state at all, which `RequestAdditionalTime` refuses as it refuses the running
  state. It expects no exception, a return within 5 seconds, the shutdown line,
  the work's own line for a stop during the startup wait, no unfinished-stop
  warning and no error. On Linux `ServiceBase` cannot be constructed, so it is
  skipped there; it runs on the development machine and in CI, which run on
  Windows. Before the correction it failed on the development machine with the
  exception above.
- **OnShutdown never reaches RequestAdditionalTime, and does reach the
  lifecycle's stop.** It reads the compiled service: every method reachable
  from `OnShutdown` through calls into the service's assembly, and every method
  outside it that those call. A call written against a base class's virtual
  method is followed into every override of it in the assembly, because
  `OnShutdown() => OnStop()` compiles to a call naming `ServiceBase.OnStop`.
  The first version of this test did not do that, so its first assertion
  passed against the old code for the wrong reason; running it against the old
  code showed that, before anything was relied on.
- **OnStop still asks for more time, where ServiceBase allows it.** The same
  reading from `OnStop`.

Seven changes made on purpose to the corrected code, each caught by the two
tests that read the compiled service: `OnShutdown` calling `OnStop` again;
`OnShutdown` asking for more time itself; `OnShutdown` only logging; `OnStop`
not asking for more time; `OnStop` not stopping the work; `StopTheWork` asking
for more time; `StopTheWork` never calling `ServiceLifecycle.Stop` (caught by
both). What only the Windows test can catch, the shutdown line's wording, the
5-second bound and the work actually stopping, was not broken on purpose: it
cannot run in Claude's workspace.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12,
built as described in earlier findings: the six test projects that build
directly, and the service and its tests through two scratch projects outside
the repository, which here reference the SDK's copy of
System.ServiceProcess.ServiceController. The service suite: 115 pass and the
Windows test is skipped. On the development machine, before the correction,
the service suite reported 114 passed and 2 failed, the two that the
correction answers. The numbers after it are taken by Edwin West before the
commit, and the commit message gives them.

## What a run at a real restart would need to show

How a build with the correction gets onto a machine, and when that machine is
restarted, is Edwin's to decide, and is to be put to him. What would show it:
the service's log holding, before the boot, `stopping: Windows is shutting
down.`, the goodbye line, the summary and `SecretPrinter stopped.`; and no
application error from `SecretPrinter.Service.exe` in the System log. With the
printer offered at the time, a capture on the client adapter would show the
goodbye leave.

## What changed

- `src/SecretPrinter.Service/WindowsService.cs`, as above.
- `tests/SecretPrinter.Service.Tests/ShutdownTests.cs` (new) and `Program.cs`,
  which registers it.
- `README.md`: the new row `REQ-LIF-009`; dated notes on `REQ-LIF-001`,
  `REQ-LIF-002` and `REQ-LIF-003`; a Known problems entry on releases 0.1.0 and
  0.2.0, and the dated note at the head of that section.

## What this does not show

- **A real restart with the correction,** or how long Windows then waits.
- **The goodbye at a shutdown on the wire.**
- **What the service does at a shutdown that arrives while a stop request is
  already under way,** or the reverse.
