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

*Corrected by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21, from measurements on FIOS-STB-01: `LocalService` moved from
unverified to verified; the service install rewritten around the two things that
actually broke it (a build under a user profile, and PowerShell's quoting of
`sc.exe`); the firewall rules tied to the installed path and the network profile;
a new section on the printer-side network dropping; and the claim under
Uninstalling that goodbye records make the printer disappear promptly, which an
iPhone contradicted. Reviewed by a human before merge.*

*The limit "the service will not start while the printer-side adapter is
down" replaced, when REQ-LIF-008 changed startup to wait instead, by Claude
(Anthropic model, Claude Opus 5.5) at the direction of Edwin West, 2026-09-22,
and marked as run on FIOS-STB-01 the same evening. Reviewed by a human before
merge.*

*The sentence under Uninstalling that read "The goodbyes go out over IPv4 only,
and iPhones ask over IPv6." corrected by Claude (Anthropic model, Claude Opus
5.5) at the direction of Edwin West, 2026-09-24. The warning after it relied on
reading it as "only over IPv6". An iPhone was measured sending the same mDNS
questions over IPv4 and IPv6, with only the IPv6 copies reaching the service's
machine ([finding](findings/2026-09-24-the-ipv6-only-claim-was-in-more-places.md)).
Reviewed by a human before merge.*

*Under Uninstalling, a sentence added by Claude (Anthropic model, Claude Opus
5.5) at the direction of Edwin West, 2026-09-25, reporting that an iPhone
dropped the printer within about three seconds of a stop. The last sentence
there read "Why iOS kept it, and how long it actually stays, are not
established."; it now limits both to the 2026-09-21 occasion. Reviewed by a
human before merge.*

*Under "When the printer-side network drops", the bullet on reopening the
printer-side socket (`REQ-RES-009`) added by Claude (Anthropic model, Claude
Opus 5.5) at the direction of Edwin West, 2026-09-25, and its run on
FIOS-STB-01 added 2026-09-26. This note was left out when the bullet was
written, and added 2026-09-26. Reviewed by a human before merge.*

*In the same section, WLAN event `4003` added to the command and named, by
Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
2026-09-26, as item 4 of
[the 2026-09-18 finding](findings/2026-09-18-the-printer-side-interface-goes-away.md)
asked. Reviewed by a human before merge.*

*In the same section, the paragraph and commands on a link that fails with no
WLAN event added by Claude (Anthropic model, Claude Opus 5.5) at the direction
of Edwin West, 2026-09-27. Reviewed by a human before merge.*

*In the same section, a sentence on recovery from a lost membership added to the
`REQ-RES-009` bullet by Claude (Anthropic model, Claude Opus 5.5) at the
direction of Edwin West, 2026-09-27. Reviewed by a human before merge.*

*The section "When another device uses the printer's name" added by Claude
(Anthropic model, Claude Opus 5.5) at the direction of Edwin West, 2026-09-28,
for REQ-ADV-023 and REQ-ADV-024, and its run on FIOS-STB-01 added 2026-09-29.
Reviewed by a human before merge.*

*The development printer's host name under Configuring redacted by Claude
(Anthropic model, Claude Opus 5.5) at the direction of Edwin West, 2026-09-29
([finding](findings/2026-09-29-the-audits-redaction-was-undone.md)). Reviewed by
a human before merge.*

*The section "Where the program comes from", the release form of the probe step
and of the install step, and the first paragraph under "Updating an installed
service" added by Claude (Anthropic model, Claude Opus 5.5) at the direction of
Edwin West, 2026-10-04, after a folder made by `publish-release.ps1` was
installed and run on a machine with no .NET
([finding](findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)). Reviewed by a human before merge.*

*The section "Checking a release before you install it" added by Claude
(Anthropic model, Claude Opus 5.5) at the direction of Edwin West, 2026-10-04,
for `REQ-DIST-005`, after the release workflow signed a trial build and the
commands in it were run on that build
([finding](findings/2026-10-04-the-release-workflow-signed-a-build.md)). Reviewed by a human before merge.*

*Corrected by Claude (Anthropic model, Claude Opus 5.5) at the direction of
Edwin West, 2026-10-06, after the steps were followed on a signed trial build
and three faults were found
([finding](findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)):
the command under Configuring and the two under Running given the `.\` they
need; step 4 told where the printer's address and host name come from, and to
run the probe first; and a paragraph added under Configuring on the instance
name that the probe prints twice. Also added: the form PowerShell writes the
example file in, and the results of that day under "Where the program comes
from", "Checking a release before you install it" and step 4. Reviewed by a
human before merge.*

*Corrected again by Claude (Anthropic model, Claude Opus 5.5) at the direction
of Edwin West, 2026-10-06, after a signed trial build was installed as a service
in place of an earlier one, started at boot and uninstalled
([finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
Under "Updating an installed service": steps for an installation made from a
release added, and the steps for one built from source put under their own
heading, in place of a first paragraph that said updating from one release to
the next had not been exercised. Under "Uninstalling": the commands that remove
the service's registration, the program folder and the data folder added, and
"The service leaves nothing else behind" changed to "The service itself". In
step 4: "the `ADDRESS` line" corrected, because the probe prints one line for
each address. Under "As a Windows service": what `sc.exe stop` prints for a
service that is already stopped, and the measurement of a start at boot. Also
added: that day's result under "Where the program comes from". Reviewed by a
human before merge.*

*Under "What you must do that the service will not", "any shipped assembly"
corrected to "any assembly of the service", and a sentence on the probe added,
by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
2026-10-06. The probe is shipped too, and until that day no test read it
([finding](findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md)).
Reviewed by a human before merge.*

*Corrected by Claude (Anthropic model, Claude Opus 5.5) at the direction of
Edwin West, 2026-10-07, when the README was read against the code and the
findings ([finding](findings/2026-10-07-the-readme-was-read-against-the-code.md)).
Under "Allow inbound mDNS": the sentence that Windows Firewall "will usually
prompt on first run", which nothing had measured. Under Configuring: the
sentence that the service "does not start if either does not answer", which
stopped being true on 2026-09-22; and a paragraph on IPv6 on the client-side
adapter. Under Running: the meaning of exit code 4. Under "As a Windows
service": a line that removes the second, unused copy of the configuration
which the install step leaves beside the program when the configuration was
made in the release folder; and a paragraph on a service that fails after it
has started. Under Privileges: the second machine `LocalService` has run on.
New section: "What you will see on an iPhone". The added line has not been
run by anyone yet.
Reviewed by a human before merge.*

*Corrected by Claude (Anthropic model, Claude Opus 5.5) at the direction of
Edwin West, 2026-10-07, later the same day, when the code was changed so that a
service which fails after it has started stops, and ends its process
([finding](findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md)).
Under Running: the row for exit code 4 and the paragraph below the table.
Under "As a Windows service": the paragraph on a service that fails after it
has started, rewritten. What that paragraph describes has not been run under
Windows.
Reviewed by a human before merge.*

*Corrected by Claude (Anthropic model, Claude Opus 5.5) at the direction of
Edwin West, 2026-10-07, later again, when the example configuration stopped
printing a UUID, as Edwin West decided that day
([finding](findings/2026-10-07-the-example-configuration-handed-every-user-the-same-uuid.md)).
Step 3, and one paragraph under Configuring. The program's new behaviour is
tested; the steps as now written have not been walked on a machine.
Reviewed by a human before merge.*

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

## Where the program comes from

**From a release, once there is one.** No release has been published yet. A
release will be a folder holding `SecretPrinter.Service.exe`,
`SecretPrinter.Probe.exe` and the .NET runtime they need, so that nothing else
has to be installed (`REQ-DIST-011`). `publish-release.ps1` makes that folder. On
2026-10-04 one was unpacked on a machine running Windows 11 with no .NET
installed, and printed for an iPhone, first from a console window and then
installed as a service by the steps below
([the finding](findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)).
On 2026-10-06 a folder that the release workflow had signed was checked,
configured by hand and run from a console window on the same machine, by the
steps in this document, and printed
([the finding](findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).
Later that day the signed folder was installed there as a service, in place of
the folder of 2026-10-04, started by Windows at boot, and then uninstalled
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).

**From source, today.** Clone the repository and install the .NET 10 SDK.

Where a step differs between the two, both forms are shown. Nothing a release
needs uses the `dotnet` command.

---

## Checking a release before you install it

No release has been published yet. When one is, it will be on the repository's
Releases page as two files: `SecretPrinter-<version>-win-x64.zip`, and a small
file beside it with the same name ending in `.sha256`. Make both checks below
before you install anything. They need only PowerShell.

**A SecretPrinter file that is unsigned, or signed under any name but the one
below, is not an official release.** Do not install it, wherever it came from
and whatever it is called. The one exception is a build you made yourself from
source: that is unsigned, and it is yours.

### 1. The download is the file that was published

In the folder that holds the two files:

```powershell
Get-FileHash -Algorithm SHA256 .\SecretPrinter-<version>-win-x64.zip
Get-Content .\SecretPrinter-<version>-win-x64.zip.sha256
```

The 64 characters must be the same in both. If they are not, the download is
damaged or is not the published file.

### 2. Every program file is signed, and SecretPrinter's are signed by this project

Unpack the zip into a new folder, then:

```powershell
Get-ChildItem <folder> -Recurse -File | Where-Object { $_.Extension -in '.exe','.dll' } | ForEach-Object { $s = Get-AuthenticodeSignature $_.FullName; [pscustomobject]@{ Ours = $_.Name -like 'SecretPrinter*'; Status = $s.Status; Signer = $s.SignerCertificate.Subject } } | Group-Object Ours, Status, Signer | Format-Table Count, Name -AutoSize -Wrap
```

What a release shows:

- **Every line says `Valid`.** Any other status, on any file, means stop.
- **The lines beginning `True` are SecretPrinter's own files.** There is one
  such line, and its signer is exactly:

  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`

- **The lines beginning `False` are the .NET runtime,** which Microsoft signs.
  Their signers all end `O=Microsoft Corporation, L=Redmond, S=Washington, C=US`.

Compare the signer's name, not a certificate thumbprint. The service that signs
SecretPrinter issues a new certificate every day, so a thumbprint from one
release will not match the next; the name does not change. Each signature
carries a timestamp, which is what keeps it valid after that day's certificate
has expired.

Windows shows the same thing without PowerShell: right-click
`SecretPrinter.Service.exe`, choose **Properties**, and open the **Digital
Signatures** tab.

**What the signature tells you,** and what it does not: that the files came out
of this repository's release workflow, under the signing account of the person
named, and have not been changed since. It says nothing about whether the
software is free of mistakes. The README is where this project says what it
does and what is known to be wrong with it.

These two commands were run on 2026-10-04 on a trial build that the release
workflow had signed, with the results above
([the finding](findings/2026-10-04-the-release-workflow-signed-a-build.md)).
They were run again on 2026-10-06 on a second machine, on the same build, with
the same results. By then the certificate that had signed SecretPrinter's files
had expired, at 03:27:46Z that day, and its signature on
`SecretPrinter.Service.exe` read `Valid` at 04:39:33Z
([the finding](findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).
That build was not a release.

---

## What you must do that the service will not

The service creates no firewall rules, writes no registry keys, changes no
routes and enables no IP forwarding. Those are `REQ-SEC-004`, `-005` and `-007`,
and they are enforced by a test that fails if any assembly of the service so
much as references a type capable of them. The probe, which a release also
holds, is held to the same by tests of its own (`REQ-SEC-016`).

The consequence is that a few things are your job.

### 1. Allow inbound mDNS

SecretPrinter must receive UDP 5353 on both networks. Add the rule yourself.
Windows Firewall may ask when a program first listens, if it is run from a
console window; a service has no one to answer, and whether Windows asks for
this program has not been measured. (Until 2026-10-07 this said Windows
Firewall "will usually prompt on first run". On 2026-10-06 a machine on which
the program had been run from a console window on two days was read, and no
firewall rule made by Windows named it; whether Windows had asked was not
recorded.)

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

**Both rules name the program, so they must name the file the service actually
runs.** A rule scoped to a build under your profile does nothing for the same
program published to `C:\Program Files\SecretPrinter`. On FIOS-STB-01 the rules
were first created against the build folder and had to be repointed when the
service moved (2026-09-18). If you move or reinstall it, repoint them:

```powershell
# Run as Administrator.
Set-NetFirewallRule -DisplayName "SecretPrinter mDNS" -Program "C:\Program Files\SecretPrinter\SecretPrinter.Service.exe"
Set-NetFirewallRule -DisplayName "SecretPrinter IPP" -Program "C:\Program Files\SecretPrinter\SecretPrinter.Service.exe"
```

**And a `Private` rule does nothing on a network Windows has classified
`Public`.** Check both networks, and check what the rules actually say rather
than what you meant them to say:

```powershell
Get-NetConnectionProfile | Format-Table InterfaceAlias, NetworkCategory -AutoSize
Get-NetFirewallRule -DisplayName "SecretPrinter*" | ForEach-Object { [pscustomobject]@{ Name = $_.DisplayName; Enabled = $_.Enabled; Profile = $_.Profile; Program = ($_ | Get-NetFirewallApplicationFilter).Program; Port = ($_ | Get-NetFirewallPortFilter).LocalPort } } | Format-Table -AutoSize
```

Measured on FIOS-STB-01, 2026-09-21: both rules enabled, `Private`, scoped to
`C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`, on 5353 and 631, and
both networks `Private`. `Get-NetConnectionProfile` has been seen not to list
every connected network, so a missing row is not proof of anything.

Without the IPP rule, a capture on 2026-09-06 showed connections to 631 getting
no answer at all — no refusal — which
[that finding](findings/2026-09-06-ipv6-mdns-transport.md) attributes to the
firewall. That looks exactly like a printer that is off, which is why it is worth
checking first.

### 3. Generate a UUID

Configuration requires one and the service will not invent it, because a
generated default would mean every installation advertising the same identity:

```powershell
[guid]::NewGuid()
```

The value goes in `advertise.uuid`. The example configuration prints that entry
empty, and the service refuses to start until it holds a UUID.

Until 2026-10-07 the example printed a fixed UUID,
`b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94`, and that value loaded, which is the very
thing the sentence above says the service avoids. A configuration that still
holds it goes on loading, and advertises the same UUID as every other
installation that kept it. To stop sharing it, put one of your own in its
place and restart the service. What a device that has already printed through
the proxy makes of a changed UUID has not been measured
([finding](findings/2026-10-07-the-example-configuration-handed-every-user-the-same-uuid.md)).

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

**Run the probe first.** The command below needs the printer's address, its
host name and its port, and the probe prints all three.
[Configuring](#configuring) shows how to run it. Look under the `INSTANCE` line
that ends `._ipps._tcp.local`:

- the `SRV` line gives the host name, as `host=`, and the port, as `port=`;
- the `ADDRESS` lines give the host's addresses, after `->`, one line for each
  address. The development printer shows four: an IPv4 address and three IPv6
  addresses. The one you need is the IPv4 address, four numbers with dots
  between them. If the probe could not find an address, there is one line and
  it says `(not resolved in this run)`.

Until 2026-10-06 this said "the `ADDRESS` line", as if there were one. The lines
were first looked at that day
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).

Then run this from the machine that will run SecretPrinter, as one line:

```powershell
$t=[Net.Sockets.TcpClient]::new('<printer-ipv4>',631); $s=[Net.Security.SslStream]::new($t.GetStream(),$false,{$true}); $s.AuthenticateAsClient('<printer-host-name>'); $d=$s.RemoteCertificate.GetRawCertData(); 'SHA256 ' + ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($d)) -replace '-',''); 'SHA1   ' + ([BitConverter]::ToString([Security.Cryptography.SHA1]::Create().ComputeHash($d)) -replace '-',''); $s.RemoteCertificate.Subject; $s.SslProtocol; $s.Dispose(); $t.Dispose()
```

It opens a TLS connection to the printer, sends nothing after the handshake,
hashes the certificate the printer presented, prints the result with the
certificate's subject and the TLS version, and closes the connection.

- Replace `<printer-ipv4>` with the printer's address on the printer-side
  network, from the probe's `ADDRESS` line that shows an IPv4 address.
- Replace `<printer-host-name>` with the printer's host name: what the probe's
  `SRV` line shows after `host=`, up to and not including `.local`. The
  development printer was measured with `EPSON000000` (its host name, with the
  low three bytes redacted as elsewhere in this repository). Whether other
  printers care what name is given here has not been tested.
- `631` is the port the development printer uses for TLS, where it advertises
  `_ipps._tcp`. Another printer may use another port; use the one on the
  probe's `SRV` line.

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
It was measured again on 2026-10-06, from a second machine running Windows 11,
with the host name taken from the probe's `SRV` line, and gave the same two
fingerprints
([the finding](findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).

---

## Configuring

In the folder that holds the program:

```powershell
.\SecretPrinter.Service.exe --print-example-config > secretprinter.json
```

The `.\` is needed. PowerShell does not run a program from the current folder
by its bare name, and until 2026-10-06 this command and the two under
[Running](#running) were printed here without it. Run that way, this one failed
and made no file
([the finding](findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).

Windows PowerShell's `>` writes that file as UTF-16. The service reads it in
that form, and on the machine where this was measured Notepad kept the form
when the file was edited and saved (2026-10-06, Windows 11; the same finding).

Interfaces are named the way Windows names them, not by address — addresses move
on DHCP and a configuration that goes stale overnight is worse than useless. To
see yours:

```powershell
Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object Name, InterfaceDescription
Get-NetIPAddress -AddressFamily IPv4 | Select-Object InterfaceAlias, IPAddress
```

To find the printer's instance name, use the probe. From a release folder:

```powershell
.\SecretPrinter.Probe.exe --interface <printer-side-ipv4>
```

From a clone of the repository, which needs the .NET SDK:

```powershell
dotnet run --project tools\SecretPrinter.Probe -- --interface <printer-side-ipv4>
```

Take the `INSTANCE` lines verbatim, spaces included. Two go into the
configuration. `printerInstance` is the one ending in `._ipp._tcp.local`: the
printer's capabilities, which the advertisement is built from, come from it.
`printerIppsInstance` is the one ending in `._ipps._tcp.local`: every connection
to the printer goes to the address and port it resolves to. Neither is worked
out from the other, and each is refused if it does not end in its own service
type. The service resolves both at startup. If either does not answer, it
starts, offers nothing, and keeps asking (`REQ-LIF-008`). A misspelled name
looks exactly like a printer that is switched off, and the log says so. (Until
2026-10-07 this said the service "does not start if either does not answer",
which was so until 2026-09-22.)

**Each client-side adapter needs IPv6 switched on.** The service answers mDNS
over IPv6 as well as IPv4 on the client side (`REQ-ADV-018`), and it is not
built to run without that. To check:

```powershell
Get-NetAdapterBinding -Name '<adapter name>' -ComponentID ms_tcpip6
```

This is not checked when the configuration is loaded. Read from the code and
never run: with IPv6 off on a client adapter, the service would wait for the
printer as usual and then stop with an error naming the adapter. The printer
side needs only IPv4.

**The name ending `._ipp._tcp.local` can be printed twice.** The probe prints an
`INSTANCE` line under a `SERVICE TYPE` heading for each service type it asked
about, and it asks about `_universal._sub._ipp._tcp.local` as well as
`_ipp._tcp.local`. The development printer is listed under both, with the same
name. It is one name; copy it once.

The example leaves `printerCertificateSha256` empty on purpose, and the service
refuses to start until it holds the fingerprint from
[step 4](#4-measure-the-printers-certificate-fingerprint). A value that merely
looked like a fingerprint would load, and the mistake would only show when a job
was attempted.

It leaves `advertise.uuid` empty too, since 2026-10-07, and the service refuses
to start until it holds the UUID from [step 3](#3-generate-a-uuid). With
neither filled in, the refusal lists both.

Every setting that decides what is advertised or where traffic goes is required.
Start it and read the errors: all problems are reported in one run, each naming
the setting at fault.

---

## Running

```powershell
.\SecretPrinter.Service.exe --config secretprinter.json
```

Add `--log-file` to keep a copy of everything it reports:

```powershell
.\SecretPrinter.Service.exe --config secretprinter.json --log-file C:\ProgramData\SecretPrinter\secretprinter.log
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
| 4 | The service could not start, or could not go on: a socket could not be opened, an interface could not be used, or a part of the running service failed. Run from a console, the last error in the log begins `Stopped because of an error:`. |
| 5 | The log file could not be opened. |

A printer that does not answer, and a printer-side adapter that is down, are
not exits: the service waits for them (`REQ-LIF-008`). Until 2026-10-07 the
row for exit code 4 read "An interface, socket or the printer was
unavailable", and so did the program's own `--help` text; both now say what
is above. Since the same day a part of the service that fails while it is
running ends the whole service with this code. Before, the other parts ran on
([finding](findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md)).
Under `--service` the process ends with the same code; see "As a Windows
service" below for what is and is not known about that.

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

That grant is **verified** on FIOS-STB-01, where the service has run as
`LocalService` with its log in that folder since 2026-09-18
([the finding](findings/2026-09-18-running-as-localservice.md)). If it is wrong
on your machine, the service stops immediately with exit code 5 and says which
file it could not open. The finding also records what the grant allows beyond
the log: if your configuration file sits in the same folder, the service account
can modify it too, although nothing in the service writes it.

**Install the program where `LocalService` can read it.** A build under your own
profile — `C:\Users\you\...\bin\Release\...` — grants access to you, to
administrators and to `SYSTEM`, and not to `LocalService`. The service control
manager then cannot launch the process at all: `sc.exe start` fails with error
5, *Access is denied*, even from an elevated prompt, and the log stays empty
because nothing ran to write it. That happened on FIOS-STB-01 on 2026-09-18.
Put the program in `C:\Program Files\SecretPrinter` and keep the configuration in
`C:\ProgramData\SecretPrinter`. From a release folder, copy it there:

```powershell
# Run as Administrator.
Copy-Item <release-folder> "C:\Program Files\SecretPrinter" -Recurse
Copy-Item <your>\secretprinter.json "C:\ProgramData\SecretPrinter\secretprinter.json"
```

**If you made the configuration in the release folder,** as
[Configuring](#configuring) has you do, the first command copied it into
`C:\Program Files\SecretPrinter` with everything else. Nothing reads that
copy: the service is registered below to read the one in
`C:\ProgramData\SecretPrinter`. Remove it, so that there is one
configuration and no doubt about which is in use:

```powershell
# Run as Administrator.
Remove-Item "C:\Program Files\SecretPrinter\secretprinter.json"
```

Until 2026-10-07 this step did not say so. The stray copy was noticed by
reading the steps, not on a machine: the installs measured so far were made
from a folder that held no configuration. The `Remove-Item` line has not been
run yet.

`C:\Program Files\SecretPrinter` must not exist yet. If it does, `Copy-Item`
puts the release folder inside it and not in its place, and the service path
below then points at nothing. On the machine this was measured on, 2026-10-04,
it did not exist, and all 216 files arrived
([the finding](findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)).

From a clone of the repository, publish there instead:

```powershell
# Run as Administrator.
dotnet publish <repo>\src\SecretPrinter.Service -c Release -o "C:\Program Files\SecretPrinter"
Copy-Item <your>\secretprinter.json "C:\ProgramData\SecretPrinter\secretprinter.json"
```

**Register it with `--%`.** From Windows PowerShell, the obvious form —
variables and backtick-escaped quotes — failed on 2026-09-18: the space in
`Program Files` reached `sc.exe` as a broken argument list, `sc.exe` printed its
usage text, and no service was created. `--%` tells PowerShell to stop parsing
and hand the rest of the line to `sc.exe` exactly as written. Variables are not
expanded after it, so the paths must be written out:

```powershell
# Run as Administrator. One line.
sc.exe --% create SecretPrinter binPath= "\"C:\Program Files\SecretPrinter\SecretPrinter.Service.exe\" --config \"C:\ProgramData\SecretPrinter\secretprinter.json\" --log-file \"C:\ProgramData\SecretPrinter\secretprinter.log\" --service" obj= "NT AUTHORITY\LocalService" start= auto
```

Then check what was actually registered before starting it:

```powershell
sc.exe qc SecretPrinter
```

`BINARY_PATH_NAME` should show the program path in quotes followed by the three
arguments, and `SERVICE_START_NAME` should read `NT AUTHORITY\LocalService`.
That is exactly what FIOS-STB-01 reports, measured 2026-09-21.

```powershell
# Run as Administrator.
sc.exe start SecretPrinter
sc.exe query SecretPrinter      # expect STATE : 4 RUNNING
```

Starting and stopping a service needs an elevated prompt. From an unelevated
one, `sc.exe start` also fails with error 5, for a different reason: your
session's rights, not the service's.

**Do not omit `obj=`.** Without it `sc.exe` runs the service as **LocalSystem**,
the most privileged account on the machine. SecretPrinter was measured to need
nothing beyond standard-user privileges, so LocalSystem asks for far more than
it uses. This was a real mistake in an earlier version of these instructions;
see [the finding](findings/2026-09-04-windows-service-run.md).

`LocalService` is **verified**: on FIOS-STB-01 the service has run under it since
2026-09-18, sharing 5353, joining both multicast groups, listening on 631,
opening TLS to the printer and printing
([the finding](findings/2026-09-18-running-as-localservice.md)). Not LocalSystem.

**`start= auto` starts the service when Windows starts.** Measured on
2026-10-06 on a release install, on Windows 11: after a restart of Windows, the
service's first log line came seven seconds after the boot time Windows
reports. The printer-side network was not connected on that machine. The
service waited, logging why, and offered the printer three seconds after the
adapter became usable, with no restart
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
A boot at which the client-side adapter is not yet ready has not been measured.

**If the service fails after it has started, it stops and its process ends.**
Changed in the code on 2026-10-07 and not yet run under Windows. When
something fails after the configuration has been accepted, the service stops
everything it was doing, sends its goodbye, and logs an error line beginning
`Service stopped because of an error`, then one beginning `Ending the process
with exit code 4`. If it was one part of a running service that failed, such
as a listener that could not be opened, an error line beginning `A part of the
service failed` comes before those two. The process then ends, so that Windows
does not go on showing a failed service as running.

Not measured: what `sc.exe query` shows afterwards, what `sc.exe start` prints
when the failure comes within the start, which event Windows records, and
whether a recovery action set with `sc.exe failure` then runs. The steps above
set no recovery action, so a service that ends this way stays stopped until it
is started again. If the printer is not offered, read the end of the log
before anything else. Before that date the failure was logged and nothing
more happened: the other parts of the service ran on, the process stayed, and
Windows was told nothing
([finding](findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md)).
This is listed under Known problems in the README.

To remove it:

```powershell
# Run as Administrator.
sc.exe stop SecretPrinter
sc.exe delete SecretPrinter
```

If the service is already stopped, `sc.exe stop` prints
`[SC] ControlService FAILED 1062:` and `The service has not been started.` That
is expected, and `sc.exe delete` still removes it, printing
`[SC] DeleteService SUCCESS` (measured 2026-10-06, the same finding). This
removes the registration only. [Uninstalling](#uninstalling) lists everything
an install puts on the machine.

### Updating an installed service

There are two procedures: one for an installation made from a release folder,
and one for an installation built from source.

#### From a release

An update replaces the program folder and nothing else. The service's
registration, your configuration, the log and the firewall rules all name the
same paths afterwards, so none of them is touched.

**1. Get the new release and check it.** Download its two files, make both
checks under
[Checking a release before you install it](#checking-a-release-before-you-install-it),
and unpack the zip into a new folder.

**2. Stop the service and remove the old program folder.**

```powershell
# Run as Administrator.
Stop-Service SecretPrinter
Remove-Item "C:\Program Files\SecretPrinter" -Recurse
Test-Path "C:\Program Files\SecretPrinter"
```

Go on only when the last line prints `False`. The old folder is removed, not
copied over, for two reasons. Copying over a folder adds and overwrites and
never removes, so a file the new version no longer has would stay behind. And
`Copy-Item` into a folder that exists puts the new folder inside it, as the
install step above says.

**3. Copy the new folder in.**

```powershell
# Run as Administrator.
Copy-Item <new-release-folder> "C:\Program Files\SecretPrinter" -Recurse
```

**4. Confirm what is installed, before you start it.** Run the signature check
from "Checking a release before you install it" again, with
`"C:\Program Files\SecretPrinter"` as the folder. Then read the version stamped
in the program:

```powershell
(Get-Item "C:\Program Files\SecretPrinter\SecretPrinter.Service.dll").VersionInfo.ProductVersion
```

It prints the version, a `+`, and the commit the files were built from. The
part before the `+` must be the version of the release you downloaded. The
trial build this was measured on printed
`0.1.0-rc.2+3bcdd4c7784aef0355a1d2d93860039e93f69278`, and `3bcdd4c` is the
commit its tag names.

**5. Start the service, wait about fifteen seconds, and read the end of its
log.**

```powershell
# Run as Administrator.
Start-Service SecretPrinter
```
```powershell
Get-Content C:\ProgramData\SecretPrinter\secretprinter.log -Tail 5
```

A service that is offering the printer ends its start-up with `Announced.
Answering queries.` and an `Accepting print jobs on` line for each address it
listens on. If the last line begins `Waiting for the printer-side interface` or
`The printer did not answer at startup`, the update itself worked and the
service is waiting for its printer side; see
[When the printer-side network drops](#when-the-printer-side-network-drops).

**To go back** to the version you had, follow the same steps with that
release's zip. Keep it until the new version has printed. Going back has not
been tried.

These steps were followed on 2026-10-06, on Windows 11, and an iPhone printed
through the updated service
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
Three limits, all in that finding. Neither folder was a release: the old one
was an unsigned folder built by hand, and the new one a trial build the release
workflow had signed. The steps were run before this text was written, from
commands Claude gave Edwin West one at a time, so nobody has yet followed them
from this document. And that service started in the waiting state described
under step 5, because the machine's printer-side network had not been
connected; it offered the printer by itself once it was.

#### From source

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
git -C <repo> pull
```
```powershell
dotnet publish <repo>\src\SecretPrinter.Service -c Release -o "C:\Program Files\SecretPrinter"
```

The pull is not optional when the machine running the service is not the machine
the change was written on. Publishing without it installs the older code under a
new timestamp, which looks exactly like a successful update.
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

## What you will see on an iPhone

Open the print screen and tap **Printer**. The proxy is listed under the name
you gave it in `advertise.instanceName`; the example's is
`SecretPrinter (ET-3760)`. In its list of known printers an iPhone has shown,
with that name, a note that begins `SecretPrinter proxy. Capabilities read
from the printer at` and gives a time.

**On an iPhone that has printed to the printer directly, the name may change
when you pick it.** This has been seen on one iPhone, which had printed to the
development printer on the printer's own network before:

- its list held two rows, `SecretPrinter (ET-3760)` and `EPSON ET-3760
  Series`;
- tapping the first turned the selection into `EPSON ET-3760 Series`;
- the page printed, and the service's log showed every connection of that
  print relayed through the proxy.

So the row with the printer's name led to the proxy too. A second device,
which had not printed to the printer, listed only the proxy and kept its name,
by Edwin West's account. Captures on 2026-09-29 found nothing sent by
SecretPrinter that named the printer's host, and that finding puts the label
down to what the iPhone remembered of the printer. The proxy's advertisement
does carry the printer's product string, `(EPSON ET-3760 Series)`, which the
README's REQ-ADV-009 has it copy. Why iOS does it is not established, and a
device that has never been on the printer's network was not tested. Seen on
2026-09-21, 2026-09-29 and 2026-10-06, the last time through two different
installations
([2026-09-21](findings/2026-09-21-a-second-printer-entry-on-the-iphone.md),
[2026-09-29](findings/2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md),
[2026-10-06](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).

**A print attempt can send no job while the proxy is answering.** Three
attempts on record did that, on 2026-10-01 and 2026-10-02, and in two of them
the iPhone said it could not reach the printer. Try again; a later attempt
printed on both days. The cause is not established
([finding](findings/2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md)).

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

### Under `LocalService`

`NT AUTHORITY\LocalService` is more restricted again — no network credentials,
minimal local rights. The service runs under it on FIOS-STB-01, measured
2026-09-21 and in use since 2026-09-18
([the finding](findings/2026-09-18-running-as-localservice.md)). So the claim is
now "runs as LocalService", on that machine. On a second machine, running
Windows 11, the service was registered under `LocalService`, started, and
printed, on 2026-10-04 and 2026-10-06; the account of the running process was
not read there
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
If you run it elsewhere and it differs, add what you find to
`docs/findings/`. (Until 2026-10-07 this said "It has not been measured on any
other".)

## When the printer-side network drops

SecretPrinter reaches the printer over whatever link you gave it. If that link
is Wi-Fi, measure whether it stays up. On FIOS-STB-01 it does not: two adapters
from different makers have been disconnected by their drivers, and **nothing
reconnected either of them** — the profile is set to connect automatically, and
the WLAN-AutoConfig log records no attempt, for 13 hours in one case, until a
person connected by hand
([the finding](findings/2026-09-20-the-wlan-drops-and-nothing-retries.md)).

The service does not reconnect the link and will not: changing the host's
network configuration is exactly what `REQ-SEC-007` forbids. What it does is stop
offering the printer while it cannot reach it — the advertisement is withdrawn
and the listener closed — and resume when the printer answers again
(`REQ-LIF-006`). That has run on FIOS-STB-01
([the finding](findings/2026-09-21-the-withdrawal-on-hardware.md)). Two limits to
know:

- **Coming back can be slow.** While the printer is away the service asks again
  after 1, 2, 4, 8… seconds, up to once an hour. After a long outage, the printer
  may not reappear for up to an hour after the link returns. Restarting the
  service after reconnecting brings it back at once.
- **Before each of those questions the service reopens its printer-side socket**
  (`REQ-RES-009`), on the adapter's address as it is then. On 2026-09-25 the old
  socket's membership of `224.0.0.251` was found gone after a long outage, and
  the adapter reconnecting did not bring it back
  ([the finding](findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md)).
  Without a new socket the service could not hear the printer at all, and would
  have stayed withdrawn until restarted. Versions before this change behave that
  way: after a long outage, if the printer has not reappeared within an hour of
  the link returning, restart the service. Run on FIOS-STB-01 on 2026-09-25:
  with the Wi-Fi disconnected for 33 minutes, the log said once why the socket
  could not be reopened, and after the reconnect the service reopened it and
  offered the printer again without a restart
  ([the finding](findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md)).
  On 2026-09-27 the membership was actually lost, with the adapter disabled and
  enabled while the service ran, and the reopen restored it and offered the
  printer again, also without a restart
  ([the finding](findings/2026-09-27-a-lost-membership-recovered-without-a-restart.md)).
- **Starting while the printer-side adapter is down waits rather than
  refusing** (`REQ-LIF-008`). The service runs and offers nothing, logs
  `Waiting for the printer-side interface:` with the reason, examines the
  adapter every 5 seconds, and carries on by itself once you reconnect it and
  the printer answers. Until 2026-09-22 it refused to start instead, so a reboot
  during an outage left it stopped. The adapter must still exist by name: an
  adapter that is absent altogether, such as a USB adapter that is unplugged, is
  refused when the configuration is loaded. Run on FIOS-STB-01 on 2026-09-22:
  started with the adapter down, came up 8 seconds after it was reconnected,
  and relayed a print from an iPhone
  ([the finding](findings/2026-09-22-the-withdrawn-start-on-hardware.md)).

To see whether the link has been dropping, and why:

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'Microsoft-Windows-WLAN-AutoConfig/Operational'; Id = 4003, 8000, 8001, 8002, 8003; StartTime = (Get-Date).Date } -ErrorAction SilentlyContinue | Sort-Object TimeCreated | Format-List TimeCreated, Id, Message
```

`8003` is a disconnect, and its `Reason` says whether the driver or a user ended
it. `8000`, `8001` and `8002` are a connection starting, succeeding and failing.
A `8003` from the driver with no `8000` after it is this problem. The event's
`Connection Mode` line describes how *that* connection was started, not the
profile's setting; check the profile with `netsh wlan show profile`.

`4003` is WLAN-AutoConfig itself acting on the link: its message says it
*"detected limited connectivity, attempting automatic recovery"*. On
FIOS-STB-01 on 2026-09-18 it was logged twice. The first came in the same
second as an `8003`, 20 seconds after the link had been connected by hand, and
the finding attributes that disconnect and a later one to this recovery
([the finding](findings/2026-09-18-the-printer-side-interface-goes-away.md)).
Why Windows judged that network limited was not established. A drop can also
come without it: in the log read on 2026-09-25, five disconnects were each
ended by the driver and no `4003` appeared
([the finding](findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md)).

To reconnect by hand, using the profile name that command shows:

```powershell
netsh wlan connect name="<profile>" interface="<adapter name>"
```

The WLAN log does not see every failure of the link. On FIOS-STB-01 on
2026-09-26 the printer-side adapter lost its address twice, falling back to one
in `169.254.0.0/16`, and neither time did WLAN-AutoConfig log a disconnect. The
second time the adapter was still without a usable address more than six hours
later ([the finding](findings/2026-09-27-the-printer-went-silent-and-the-join-held.md)).
So a quiet WLAN log does not show that the link stayed up. Both times the
service's own log recorded it: lookups failing at once with
`The requested address is not valid in its context.`, then a withdrawal whose
reopen failed with `Could not reopen the printer-side socket:` and the reason,
that the adapter held a `169.254` address and was not up. A start in that state
logs `Waiting for the printer-side interface:` with the same kind of reason. To
look for those lines, and at the adapter's address now:

```powershell
Select-String -Path C:\ProgramData\SecretPrinter\secretprinter.log -Pattern 'not valid in its context|Could not reopen|Waiting for the printer-side interface' | Select-Object -Last 20 | ForEach-Object { $_.Line }
Get-NetIPAddress -InterfaceAlias '<adapter name>' -AddressFamily IPv4 | Select-Object IPAddress, AddressState
```

## When another device uses the printer's name

Before it offers the printer, at startup and each time the printer comes back,
the service first asks the client network whether any other device already uses
the names it is about to advertise (`REQ-ADV-023`, RFC 6762 §8.1). If one
answers, or if one answers for those names at any time later, the service
withdraws the printer and stays withdrawn until it is restarted (`REQ-ADV-024`).
It does not rename itself. The log says so in one line beginning
`Name conflict:`, which gives the name, the record type, the address of the
device that answered, and the interface it was heard on.

To recover, find out which device has that address. If it should not be using
the name, stop it; otherwise choose a different advertised name in the
configuration. Then restart the service.

Any device on the client network can cause this by answering for the printer's
names. mDNS has no authentication, and this is accepted for a home network.
Run on FIOS-STB-01 on 2026-09-29, with a second responder on the client network
claiming the service's host name: the service logged the conflict and withdrew
within a second of hearing it; restarted, it found the conflict while probing
and offered nothing; restarted again after the other responder stopped, it
probed clear and relayed a print
([the finding](findings/2026-09-29-probing-on-hardware.md)).

## Uninstalling

Stop the process. It sends mDNS goodbye records on the way out, which ask
clients to forget the printer at once. **Do not count on that.** The goodbyes go
out over IPv4 only. An iPhone has been measured sending its mDNS questions over
both IPv4 and IPv6, but where its IPv4 does not reach this machine, as on the
network SecretPrinter was built on
([finding](findings/2026-09-24-ipv4-mdns-from-behind-the-access-point.md)), only
its IPv6 questions are answered. Whether iOS applies an IPv4 goodbye to
what it learned that way is not established. On 2026-09-21 an iPhone went on
listing the printer after a goodbye
([the finding](findings/2026-09-21-the-withdrawal-on-hardware.md)). On
2026-09-25, with the printer's side up, an iPhone dropped it within about three
seconds of a stop
([finding](findings/2026-09-25-the-iphone-drops-the-printer-on-the-goodbye.md)).
If iOS ignores a goodbye, the entry would stay until the record that lists it
expires, which is 4500 seconds — 75 minutes. Why iOS kept it on 2026-09-21, and
how long it stayed, are not established.

If it is installed as a service, remove the registration next. `sc.exe stop`
stops it if it is still running:

```powershell
# Run as Administrator.
sc.exe stop SecretPrinter
sc.exe delete SecretPrinter
```

If it was already stopped, `sc.exe stop` prints
`[SC] ControlService FAILED 1062:` and `The service has not been started.` That
is expected. `sc.exe delete` prints `[SC] DeleteService SUCCESS`.

Then remove the firewall rules you added:

```powershell
# Run as Administrator.
Remove-NetFirewallRule -DisplayName "SecretPrinter mDNS"
Remove-NetFirewallRule -DisplayName "SecretPrinter IPP"
```

The service itself leaves nothing else behind. It writes no registry keys, and
the only file it writes is the log you named with `--log-file`, which stays
where you put it until you remove it:

```powershell
Remove-Item "C:\ProgramData\SecretPrinter\secretprinter.log"
```

**The install steps in this document put two folders on the machine, and they
stay until you remove them.** The program folder:

```powershell
# Run as Administrator.
Remove-Item "C:\Program Files\SecretPrinter" -Recurse
```

And the data folder, which holds the log, your configuration file if you kept
it there, and the permission the install step granted to `LocalService`.
Removing the folder removes all three. Your configuration file is yours to keep
or delete, so copy it somewhere else first if you want it:

```powershell
# Run as Administrator.
Remove-Item "C:\ProgramData\SecretPrinter" -Recurse
```

Until 2026-10-06 this section gave only the firewall rules and the log. A
release install was uninstalled that day with those commands and the two
`sc.exe` commands, and the machine was then read. No service, no registry key
for one and no firewall rule naming the program were left. Both folders were,
the program folder with all 216 of its files. The two `Remove-Item` commands
above then removed them
([the finding](findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)).
