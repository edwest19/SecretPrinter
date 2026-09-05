# Finding: running under the service control manager

**Date:** 2026-09-04
**Tool:** `src/SecretPrinter.Service` with `--service`
**Recorded by:** Claude (Anthropic model, Claude Opus 4.5), directed by Edwin West

The service registered with Windows, started, and stopped. It also showed that
the install command being recommended asked for far more privilege than the
service needs.

## What was run

```powershell
sc.exe create SecretPrinter binPath= "`"$exe`" --config `"$cfg`" --service" start= demand
sc.exe start SecretPrinter
sc.exe query SecretPrinter
sc.exe stop SecretPrinter
```

## Result

| Step | Reported |
| --- | --- |
| `create` | `CreateService SUCCESS` |
| `start` | `START_PENDING`, then `STATE : 4 RUNNING` |
| `query` | `RUNNING (STOPPABLE, NOT_PAUSABLE, ACCEPTS_SHUTDOWN)` |
| `stop` | `STOP_PENDING` accepted |
| `query` after stop | `STATE : 1 STOPPED`, `WIN32_EXIT_CODE : 0`, `SERVICE_EXIT_CODE : 0` |

The control manager acknowledged the start rather than timing out, which is what
`ServiceLifecycle.Start` returning promptly is for, and the service reported
itself stoppable and accepting shutdown, which matches how `WindowsService` is
configured.

This matters more than a usual test because `WindowsService.cs` is the one file
in the repository that was written without ever being compiled: the environment
it was authored in has no access to nuget.org, so `ServiceBase` could not be
resolved there. This run is the first evidence it works.

## The mistake this exposed

**`sc.exe create` without an `obj=` argument runs the service as LocalSystem.**

LocalSystem is the most privileged account on a Windows machine. It has
essentially unrestricted local rights and presents the computer account on the
network.

The privilege measurement recorded in
[2026-09-04-service-runs-unelevated.md](2026-09-04-service-runs-unelevated.md)
established that SecretPrinter needs nothing beyond standard-user privileges:
binding UDP 5353, joining multicast groups, binding TCP 631 and connecting
outbound all succeeded unelevated.

So the install command being recommended asked for the maximum, for a program
measured to need the minimum. Nothing in the service exploited that — it cannot,
having no reference to any type that could touch the registry, start a process
or make an HTTP request — but requesting privilege that is not needed is a
defect in the instructions, and it is the opposite of what `REQ-SEC-006`
requires.

### Corrected

`docs/operating.md` now specifies an account explicitly:

```powershell
sc.exe create SecretPrinter `
    binPath= "`"$exe`" --config `"$cfg`" --service" `
    obj= "NT AUTHORITY\LocalService" `
    start= auto
```

**`LocalService` under this configuration is not yet verified.** It is more
restricted than the standard-user account the measurement used — no network
credentials, minimal local rights — and while there is good reason to expect it
works, "good reason to expect" is not a measurement. Anyone who runs it there
should record the result here.

Until then the documented options are: run as `LocalService` and check the log
shows the advertisement, or run under a dedicated low-privilege local account.
**Do not leave it as LocalSystem.**

## Shutdown reached STOPPED

The service stopped with `WIN32_EXIT_CODE : 0` and `SERVICE_EXIT_CODE : 0`.

That is meaningful rather than incidental. `WindowsService.OnStop` waits on
`ServiceLifecycle.Stop`, which cancels the host and waits up to twenty seconds
for it to finish; the host's `finally` block sends the mDNS goodbye records,
leaves the multicast groups and disposes the sockets before returning. A
shutdown that overran the timeout would have logged a warning and a failure
during startup would have set `ExitCode = 1`. Neither happened, so the shutdown
path ran to completion within its budget.

### Still not directly observed

That the goodbye records actually reached the network. Exit code 0 shows the
code path completed; it does not show the datagrams left the interface. The
remaining check is to run `SecretPrinter.Listen --interface 192.168.1.234`
alongside a service stop and watch for the TTL-zero records.

The distinction matters because a client keeps showing a printer until either a
goodbye arrives or the TTL expires, and a goodbye that was composed but never
transmitted looks identical from inside the process.
