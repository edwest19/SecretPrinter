# SecretPrinter runs as LocalService

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21, from the service's registration and log on FIOS-STB-01.
Reviewed by a human before merge.*

**Status: measured. The service is registered to run as
`NT AUTHORITY\LocalService` on FIOS-STB-01, has done so since 2026-09-18, and on
2026-09-21 it bound its sockets, resolved the printer, answered discovery and
relayed printed pages under that account. `docs/operating.md` still calls this
unverified in two places; that is corrected separately.**

## What was measured

On FIOS-STB-01, 2026-09-21, `sc.exe qc SecretPrinter`:

```
TYPE               : 10  WIN32_OWN_PROCESS
START_TYPE         : 2   AUTO_START
BINARY_PATH_NAME   : "C:\Program Files\SecretPrinter\SecretPrinter.Service.exe"
                     --config "C:\ProgramData\SecretPrinter\secretprinter.json"
                     --log-file "C:\ProgramData\SecretPrinter\secretprinter.log" --service
SERVICE_START_NAME : NT AUTHORITY\LocalService
```

(The binary path is one line in the output; it is wrapped here for reading.)

`icacls C:\ProgramData\SecretPrinter`:

```
NT AUTHORITY\LOCAL SERVICE:(OI)(CI)(M)
NT AUTHORITY\SYSTEM:(I)(OI)(CI)(F)
BUILTIN\Administrators:(I)(OI)(CI)(F)
BUILTIN\Users:(I)(OI)(CI)(RX)
```

The first line of the service's log file:

```
2026-09-18 19:38:07Z  INFORMATION  SecretPrinter starting.
```

## What LocalService was shown to be enough for

Under that registration, on 2026-09-21 (service started 20:17:36Z, running
`d29dd4e`), the log shows the service doing everything it exists to do:

- sharing UDP 5353 and joining `224.0.0.251` on the client interface, and
  `ff02::fb` for IPv6;
- joining `224.0.0.251` on the printer interface and resolving the printer
  there;
- announcing its advertisement and answering queries;
- listening on TCP 631 on the client interface;
- opening TLS connections to the printer, checking the pinned certificate, and
  relaying two pages that printed (20:19:44Z and 20:20:53Z).

Earlier the same day, under the same account, it also withdrew and restored its
advertisement and closed and reopened its listener (`REQ-LIF-006`).

So the claim `docs/operating.md` has been waiting for can now be made: the
service runs as `LocalService`. It needs no administrator rights, no elevation
and not LocalSystem. This is the evidence behind `REQ-SEC-006`'s "least
privilege" as it applies on this machine.

## What the folder grant allows, stated plainly

The grant from `docs/operating.md`, `(OI)(CI)M` for `LocalService`, was verified
to be sufficient: the service opens and appends to its log. Two consequences of
where things live, noted rather than changed:

- **The configuration file sits in the same folder, so the same grant lets the
  service modify it.** The service never writes its configuration; nothing in
  the code opens it for writing. But the account *could*, and least privilege
  would give it read access to the configuration and modify access only to the
  log. That would mean a second folder, or a separate grant on the file.
- **Every local user can read both files.** `BUILTIN\Users:(RX)` is inherited
  from `C:\ProgramData`. The configuration holds nothing secret — the pinned
  fingerprint is public by nature. The log holds client addresses, the printer's
  address and instance name, and job timings, but no job content
  (`REQ-OBS-004`). On a single-user home machine that is harmless; on a shared
  one an operator should know it.

Neither was decided here.

## What this does not establish

- **That every start since 2026-09-18 ran as LocalService.** `sc.exe qc` shows
  the registration as it is now, not its history. The instructions it was
  created from on 2026-09-18 specified `obj= "NT AUTHORITY\LocalService"`, and
  no change to it has been recorded since; the command's own output from that
  day is not in hand.
- **Any machine but FIOS-STB-01.** The dev box has no service registered.
- **Firewall behaviour under the service.** Inbound rules are a separate,
  still-open correction to `docs/operating.md`.
