# Operating SecretPrinter

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

*Step 4, measuring the printer's certificate fingerprint, and the paragraph on
it under Configuring, added by Claude (Anthropic model, Claude Opus 5) at the
direction of Edwin West, 2026-09-15. Reviewed by a human before merge.*

*The paragraph on the two printer instance names under Configuring added by
Claude (Anthropic model, Claude Opus 5) at the direction of Edwin West,
2026-09-16. Reviewed by a human before merge.*

*Step 4 corrected when the check it describes was implemented, by Claude
(Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-17.
Reviewed by a human before merge.*

*The log file — `--log-file`, exit code 5, the folder permissions it needs, and
the corrected sentence under Uninstalling — added by Claude (Anthropic model,
Claude Opus 5) at the direction of Edwin West, 2026-09-18. Reviewed by a human
before merge.*

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

### 4. Measure the printer's certificate fingerprint

The printer refuses print jobs over unencrypted IPP, so the proxy has to reach it
over TLS. The printer's certificate is self-signed, so no certificate authority
can vouch for it; instead you tell the service which certificate to expect, by
its SHA-256 fingerprint, in `printerCertificateSha256`. The service will not start
without it (`REQ-CFG-007`). The fingerprint is compared with the certificate the
printer presents on every connection, inside the TLS handshake, with no setting
to turn the check off (`REQ-SEC-013`, `REQ-SEC-014`). A certificate that does
not match ends the handshake: the job fails, and the log names both
fingerprints, so you can tell a wrong pin from a wrong device.

**Get this value from the printer you trust, on the network you trust.** It is
the whole of the trust decision, so a fingerprint measured through something
else is a fingerprint of something else.

The service does not measure the fingerprint for you. Remembering whatever
certificate answered first would mean writing state to disk, which this project
does not do, and it would trust exactly the connection that pinning exists to
question.

Run this from the machine that will run SecretPrinter, as one line:

```powershell
$t=[Net.Sockets.TcpClient]::new('<printer-ipv4>',631); $s=[Net.Security.SslStream]::new($t.GetStream(),$false,{$true}); $s.AuthenticateAsClient('<printer-host-name>'); $d=$s.RemoteCertificate.GetRawCertData(); 'SHA256 ' + ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($d)) -replace '-',''); 'SHA1   ' + ([BitConverter]::ToString([Security.Cryptography.SHA1]::Create().ComputeHash($d)) -replace '-',''); $s.RemoteCertificate.Subject; $s.SslProtocol; $s.Dispose(); $t.Dispose()
```

It opens a TLS connection to the printer, sends nothing after the handshake,
hashes the certificate the printer presented, prints the result with the
certificate's subject and the TLS version, and closes the connection.

- Replace `<printer-ipv4>` with the printer's address on the printer-side
  network.
- Replace `<printer-host-name>` with the printer's host name. The development
  printer was measured with `EPSON3EA18A`. Whether other printers care what
  name is given here has not been tested.
- `631` is the port the development printer uses for TLS, where it advertises
  `_ipps._tcp`. Another printer may use another port; check what it advertises.

Copy the **SHA256** line's value, all 64 digits, into `printerCertificateSha256`.
Do not use the SHA1 line, and do not use the `Thumbprint` Windows shows for a
certificate: both are SHA-1, and the service refuses a SHA-1 value by name.

**`{$true}` in that command accepts any certificate.** That is correct for a
command whose only purpose is to see what the printer presents, and it is
exactly what the service must never do. Measure on the printer-side network you
trust, and if the subject does not name your printer, stop.

It is not known whether a firmware update or a factory reset gives the printer a
new certificate. If it does, measure again. The measurement of the development
printer, and why SHA-256 was chosen, are in
[findings](findings/2026-09-15-printer-requires-tls-for-job-operations.md).

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

Take the `INSTANCE` lines verbatim, spaces included. Two go into the
configuration. `printerInstance` is the one ending in `._ipp._tcp.local`: the
printer's capabilities, which the advertisement is built from, come from it.
`printerIppsInstance` is the one ending in `._ipps._tcp.local`: every connection
to the printer goes to the address and port it resolves to. Neither is worked
out from the other, and each is refused if it does not end in its own service
type. The service resolves both at startup and does not start if either does
not answer.

The example leaves `printerCertificateSha256` empty on purpose, and the service
refuses to start until it holds the fingerprint from
[step 4](#4-measure-the-printers-certificate-fingerprint). A value that merely
looked like a fingerprint would load, and the mistake would only show when a job
was attempted.

Every setting that decides what is advertised or where traffic goes is required.
Start it and read the errors: all problems are reported in one run, each naming
the setting at fault.

---

## Running

```powershell
SecretPrinter.Service.exe --config secretprinter.json
```

Add `--log-file` to keep a copy of everything it reports:

```powershell
SecretPrinter.Service.exe --config secretprinter.json --log-file C:\ProgramData\SecretPrinter\secretprinter.log
```

The file is appended to, never truncated, so a restart adds to the record rather
than erasing it. Entries are flushed one at a time, so `Get-Content -Wait` on it
shows a running service live. SecretPrinter does not create the folder and will
not start if it cannot open the file; there is no default path, because a default
would put a file somewhere you did not choose.

| Exit code | Meaning |
| --- | --- |
| 0 | Stopped cleanly. |
| 2 | Bad arguments. |
| 3 | Configuration is not usable; every problem is listed. |
| 4 | An interface, socket or the printer was unavailable. |
| 5 | The log file could not be opened. |

### As a Windows service

Add `--service` and register it. Installing a service needs elevation, even
though **running** it does not.

`--log-file` is **required** with `--service`, and the service refuses to start
without it. Under the service control manager there is no console: standard
output goes nowhere at all — not to a file, not to the event log — so a service
without a log file reports everything it knows, including why it failed, into a
void. Make the folder first and let the service account write in it:

```powershell
# Run as Administrator.
New-Item -ItemType Directory -Path "C:\ProgramData\SecretPrinter" -Force

# S-1-5-19 is NT AUTHORITY\LocalService, by SID so this works on any language.
icacls "C:\ProgramData\SecretPrinter" /grant "*S-1-5-19:(OI)(CI)M"
```

That grant has **not been verified here**; nothing on these machines has yet run
as `LocalService`. If it is wrong, the service stops immediately with exit code 5
and says which file it could not open, which is the failure you want rather than
a service that runs silently.

```powershell
# Run as Administrator.
$exe = "C:\path\to\SecretPrinter.Service.exe"
$cfg = "C:\path\to\secretprinter.json"
$log = "C:\ProgramData\SecretPrinter\secretprinter.log"

sc.exe create SecretPrinter `
    binPath= "`"$exe`" --config `"$cfg`" --log-file `"$log`" --service" `
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

### Updating an installed service

Registration points the service control manager at a path. Building a new
version does not change what is at that path, so a build alone updates nothing
— the service keeps running the binaries that are already there, and will go on
doing so through as many restarts as you give it. On 2026-09-19 this cost an
evening's measurement: a fix was built, tested and pushed, the service was
restarted several times, and every restart ran the previous day's code.

Stop the service first. Its files are locked while it runs, and the publish
fails part-written rather than cleanly.

```powershell
# Run as Administrator.
Stop-Service SecretPrinter
```
```powershell
dotnet publish <repo>\src\SecretPrinter.Service -c Release -o "C:\Program Files\SecretPrinter"
```
```powershell
Start-Service SecretPrinter
```

Then confirm that what is installed is what you just built, rather than trusting
that the copy happened:

```powershell
Get-Item "C:\Program Files\SecretPrinter\SecretPrinter.Resolution.dll" | Format-List LastWriteTime
Get-Content C:\ProgramData\SecretPrinter\secretprinter.log -Tail 5
```

The timestamp should be seconds before the service's own start line in the log.
Startup output is not a check on its own: most changes do not alter what the
service prints at startup, so an identical banner is the expected result and
proves nothing either way.

Two honest limitations of this procedure:

- **Publishing over an existing folder adds and overwrites; it never removes.**
  A file that a previous version installed and this one does not stays behind,
  and nothing reports it. For a development machine that is tolerable. A release
  install should be a clean directory.
- **This installs a framework-dependent build**, which needs a matching .NET
  runtime already on the machine. REQ-DIST-011 requires releases to be
  self-contained, so this is a development convenience and not the shape a
  release takes.

Verified on FIOS-STB-01, 2026-09-19.

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

### The file

`--log-file` writes exactly what the console receives, in the same format. Every
entry is one line: control characters in a message are escaped to `\xNN` rather
than written, so a device that names itself with an embedded line break cannot
append a line of its own choosing to your log (`REQ-OBS-010`).

The file holds no print job content, but it does hold addresses and the names of
devices seen on both networks, so it is worth treating like any other record of
who was on your network. It inherits the permissions of the folder you chose;
SecretPrinter sets none of its own.

Nothing rotates or trims it. The service logs on startup, on shutdown, and a few
lines per print job — nothing per mDNS query — so the file grows slowly, and
deleting your records on a schedule nobody asked for would be the greater sin.
Delete or move it yourself when you want to; the service reopens and appends on
its next start.

If writing to the file ever fails — a full disk, a folder that has gone away —
the service says so once on the console and keeps printing. A log that cannot be
written must not be able to fail a print job.

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

The service leaves nothing else behind. It writes no registry keys, and the only
file it writes is the log you named with `--log-file`, which stays where you put
it until you remove it:

```powershell
Remove-Item "C:\ProgramData\SecretPrinter\secretprinter.log"
```

Your configuration file is likewise yours to keep or delete.
