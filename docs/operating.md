# Operating SecretPrinter

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

Everything an operator has to do by hand, and why the software does not do it
for them.

---

## Before you install

**Print job data passes through this machine.** Your document is sent to
SecretPrinter, held in memory while it is streamed, and forwarded to the
printer. This is inherent to proxying and cannot be engineered away. What is
controlled is what happens to it here: it is never written to disk, never
parsed, and never logged — see [`REQ-PXY-003`, `REQ-PXY-004` and
`REQ-OBS-004`](../README.md#6-requirements-relay-pxy), each with a test that
reads the compiled assembly to prove the capability is absent rather than merely
unused.

**This departs from the mDNS standard on purpose.** Names ending in `.local` are
meant to be meaningful on one network segment. SecretPrinter makes one printer
visible on a second segment. That is why it confines itself to printing.

---

## What you must do that the service will not

The service creates no firewall rules, writes no registry keys, changes no
routes and enables no IP forwarding. Those are `REQ-SEC-004`, `-005` and `-007`,
and they are enforced by a test that fails if any shipped assembly so much as
references a type capable of them.

The consequence is that a few things are your job.

### 1. Allow inbound mDNS

SecretPrinter must receive UDP 5353 on both networks. Windows Firewall will
usually prompt on first run; if you dismiss it, or run as a service where no one
is there to click, add the rule yourself:

```powershell
# Run as Administrator.
New-NetFirewallRule -DisplayName "SecretPrinter mDNS" `
    -Direction Inbound -Protocol UDP -LocalPort 5353 `
    -Program "C:\path\to\SecretPrinter.Service.exe" `
    -Profile Private -Action Allow
```

### 2. Allow inbound IPP

Print jobs arrive on the port you configured, normally 631:

```powershell
New-NetFirewallRule -DisplayName "SecretPrinter IPP" `
    -Direction Inbound -Protocol TCP -LocalPort 631 `
    -Program "C:\path\to\SecretPrinter.Service.exe" `
    -Profile Private -Action Allow
```

**Use `-Profile Private`.** These rules should not apply on a public network.

### 3. Generate a UUID

Configuration requires one and the service will not invent it, because a
generated default would mean every installation advertising the same identity:

```powershell
[guid]::NewGuid()
```

---

## Configuring

```powershell
SecretPrinter.Service.exe --print-example-config > secretprinter.json
```

Interfaces are named the way Windows names them, not by address — addresses move
on DHCP and a configuration that goes stale overnight is worse than useless. To
see yours:

```powershell
Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object Name, InterfaceDescription
Get-NetIPAddress -AddressFamily IPv4 | Select-Object InterfaceAlias, IPAddress
```

To find the printer's instance name, use the probe:

```powershell
dotnet run --project tools\SecretPrinter.Probe -- --interface <printer-side-ipv4>
```

Take the `INSTANCE` line verbatim, spaces included.

Every setting that decides what is advertised or where traffic goes is required.
Start it and read the errors: all problems are reported in one run, each naming
the setting at fault.

---

## Running

```powershell
SecretPrinter.Service.exe --config secretprinter.json
```

| Exit code | Meaning |
| --- | --- |
| 0 | Stopped cleanly. |
| 2 | Bad arguments. |
| 3 | Configuration is not usable; every problem is listed. |
| 4 | An interface, socket or the printer was unavailable. |

### As a Windows service

Add `--service` and register it. Installing a service needs elevation, even
though **running** it does not.

```powershell
# Run as Administrator.
$exe = "C:\path\to\SecretPrinter.Service.exe"
$cfg = "C:\path\to\secretprinter.json"

sc.exe create SecretPrinter `
    binPath= "`"$exe`" --config `"$cfg`" --service" `
    obj= "NT AUTHORITY\LocalService" `
    start= auto

sc.exe start SecretPrinter
sc.exe query SecretPrinter      # expect STATE : 4 RUNNING
```

**Do not omit `obj=`.** Without it `sc.exe` runs the service as **LocalSystem**,
the most privileged account on the machine. SecretPrinter was measured to need
nothing beyond standard-user privileges, so LocalSystem asks for far more than
it uses. This was a real mistake in an earlier version of these instructions;
see [the finding](findings/2026-09-04-windows-service-run.md).

`LocalService` under this configuration is **not yet verified** — it is more
restricted than the account the privilege measurement used. If the service fails
to start under it, the log will say which operation was refused, and a dedicated
low-privilege local account is the fallback. Either way, not LocalSystem.

To remove it:

```powershell
sc.exe stop SecretPrinter
sc.exe delete SecretPrinter
```

---

## Reading the log

The startup log states every interface and the address it resolved to, then the
complete advertisement: every record, every TXT entry published, and every entry
dropped from the printer's own advertisement with the reason.

That last part is the point. You can confirm from the log alone that the
printer's UUID, its `Scan` flag and its `adminurl` were withheld, without
trusting this documentation and without running a packet capture:

```
  publish pdl=application/octet-stream,image/pwg-raster,image/urf,image/jpeg
  publish UUID=b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94
  DROP    Scan=T  <- REQ-ADV-006: the proxy does not relay scanning.
  DROP    mopria-certified=1.3  <- REQ-ADV-007: the printer holds that certification; the proxy does not.
```

Job entries record endpoints, byte counts and timings. Never content: the log
interface has no parameter that could accept any.

---

## Privileges: measured

**No elevation is required.**

This was measured on 2026-09-04, not assumed. Running from an ordinary
unelevated session, the service successfully bound UDP 5353 with
`SO_REUSEADDR` alongside the Windows DNS Client, joined multicast group
224.0.0.251 on both interfaces, bound TCP 631 on the client interface, and
relayed print jobs to the printer. See
[the finding](findings/2026-09-04-service-runs-unelevated.md).

Windows, unlike Unix, does not reserve ports below 1024 for privileged
processes, which is why port 631 needs nothing special.

### What that means precisely

An administrator's unelevated process on Windows runs with a **filtered token**
carrying standard-user privileges, so the measurement establishes that the
service works at standard-user level. Do not run it elevated: it needs nothing
that elevation provides.

### What has not been measured

Running under a dedicated service account such as
`NT AUTHORITY\LocalService`. That account is more restricted again — no network
credentials, minimal local rights — and is the intended target once the Windows
service question is settled. Until somebody runs it there and records the
result, the honest claim is "no elevation required", not "runs as LocalService".

If you try it, add what you find to `docs/findings/`.

## Uninstalling

Stop the process. It sends mDNS goodbye records on the way out, so the printer
disappears from clients promptly rather than lingering until its TTL expires.

Then remove the firewall rules you added:

```powershell
Remove-NetFirewallRule -DisplayName "SecretPrinter mDNS"
Remove-NetFirewallRule -DisplayName "SecretPrinter IPP"
```

The service leaves nothing else behind. It writes no registry keys and no files
apart from the configuration you created.
