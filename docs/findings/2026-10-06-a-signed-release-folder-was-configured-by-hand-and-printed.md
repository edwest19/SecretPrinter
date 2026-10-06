# A signed release folder was configured by hand and printed

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-06. Reviewed by a human before merge.*

**Status: measured. On 2026-10-06 the signed zip that the release workflow made
for the trial tag `v0.1.0-rc.2` was taken to a second machine, checked by the
two checks in [`operating.md`](../operating.md), and unpacked. A configuration
was made by hand with the steps in `operating.md`, starting from the printed
example. The signed service ran from a console window with that configuration,
and an iPhone printed a page through it. This is the first time a signed build
of SecretPrinter has been run, and the first time the configuration steps have
been followed from the first to the last on a release folder. The walk found
three faults in the instructions, which are corrected in the commit that carries
this finding. It also found that the signing certificate had expired by the
time its signature was read, and the signature still read `Valid`. No release
exists.**

No IPv6 address, MAC address or device name appears in this finding. The second
machine is called the test machine, as in
[the finding of 2026-10-04](2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md).
The printer's host name is written `EPSON000000`, its low three bytes replaced,
as elsewhere in this repository. The UUID that was generated is not given, and
the folder the work was done in is written `<walk folder>`.

**About the times.** Times ending in `Z` are UTC and come from the service's log
or from the test machine's clock. The checks and the configuration steps were
made in the half hour before the run, and the machine did not record their
times. In US Eastern time the walk began late in the evening of 2026-10-05 and
the run was at 00:22 on 2026-10-06. In UTC all of it falls on 2026-10-06.

## Why this was done

- On 2026-10-05 Edwin decided that release 0.1.0 has no configuration tool and
  that a release is configured by hand (README open question 9). That entry
  said the steps had each been run by themselves and had never been followed
  from the first to the last on a release folder.
- [The finding of 2026-10-04 on signing](2026-10-04-the-release-workflow-signed-a-build.md)
  said the signed programs had not been run.

Before the walk began, what would complete it was stated: a configuration made
only from `operating.md`, on the signed folder, is accepted when the service
starts, and a page prints through it.

## What "following the instructions" means here

Edwin ran every command on the test machine. Claude gave him the commands one
at a time, each taken from `operating.md`, and said what it expected before each
one. Edwin did not work from the document himself.

So this shows that the document's commands, used in order, produce a
configuration that works. It does not show that a person who has never seen
this project finds their way through the document.

Where a command differed from the document's text:

| Step | Difference |
| --- | --- |
| The hash check | Run after `cd` into the folder holding the two files. The document's two commands use relative paths. |
| Unpacking | `Expand-Archive`. The document says "Unpack the zip into a new folder" and names no command. |
| The adapters | The document's two commands on one line, each with `Format-Table -AutoSize` added so that both tables showed. |
| The probe | Its output was kept in a variable, and only a line count and the lines matching `INSTANCE` or `Round` were shown, so that the printer's host name stayed out of the record. Someone following the document reads the whole output. |
| The certificate | The printer's address was typed in. A statement added in front read the host name from the probe's `SRV` line. The rest was the document's command. |
| The firewall | The two rules made on this machine on 2026-10-04 were pointed at the signed program with `Set-NetFirewallRule`, the form the document gives for a program that has moved. No rule was created. |
| Running | `--log-file` named a file in `<walk folder>`. |

## The machine

The test machine of the 2026-10-04 finding, which records it as Windows 11
Home, build 26200. The version was not read again.

- **Two networks, read in this walk:** `Ethernet` at 192.168.1.207 on the
  client side and `Wi-Fi` at 192.168.12.142 on the printer's side.
- **Two virtual adapters were also up,** each with an address beginning
  `169.254.`. The configuration does not name them.
- **No .NET, read at 04:39Z, after the run:** `dotnet` was not on the path; no
  `coreclr.dll` under `C:\Program Files\dotnet`,
  `C:\Program Files (x86)\dotnet` or `%LOCALAPPDATA%\Microsoft\dotnet`; and
  `DOTNET_ROOT` was empty.
- **The unsigned folder of 2026-10-04 is still installed there as a service,**
  set to start on demand. Its state was `Stopped` when read at 04:39Z. It was
  not read before the run.

## The two checks on the download

The two files were copied by hand from the development machine, where Edwin had
downloaded them from the workflow run on 2026-10-04. They were not downloaded by
a browser on the test machine.

| File | Bytes |
| --- | --- |
| `SecretPrinter-0.1.0-rc.2-win-x64.zip` | 37,554,102 |
| `SecretPrinter-0.1.0-rc.2-win-x64.zip.sha256` | 104 |

**Check 1, the hash.** `Get-FileHash` and the `.sha256` file showed the same 64
characters,
`8A422569A5BBFB63C61385C5AE4117992CB3C3D173E8CB15B1F0D44A683C2A46`, the value
recorded on 2026-10-04.

**Unpacking** gave 216 files. `SecretPrinter.Service.exe` and
`SecretPrinter.Probe.exe` are 178,432 bytes each.

**Check 2, the signatures.** The document's command, on every `.exe` and `.dll`:

| Files | Ours | Status | Signer |
| --- | --- | --- | --- |
| 12 | True | `Valid` | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
| 185 | False | `Valid` | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 3 | False | `Valid` | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |
| 2 | False | `Valid` | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |

That is 202 program files, none unsigned, the same as Edwin read on the
development machine on 2026-10-04. This is the first time either check was made
on another machine.

### The signing certificate had expired, and its signature read `Valid`

Read on the test machine after the run, from `SecretPrinter.Service.exe`:

| | |
| --- | --- |
| The signing certificate is valid from | 2026-10-03 03:27:46Z |
| The signing certificate is valid to | 2026-10-06 03:27:46Z |
| The machine's clock at the reading | 2026-10-06 04:39:33Z |
| `Get-AuthenticodeSignature` status at the reading | `Valid` |

- **The certificate had expired 1 hour 11 minutes 47 seconds before the
  reading,** and the status was `Valid`.
- **That is one file, read at a recorded time.** The check of all 202 files was
  made earlier in the walk. By the clock of the conversation in which the walk
  was run, that was about 04:02Z, which is also after the certificate expired.
  The machine did not record that time.
- **`operating.md` says a timestamp is what keeps a signature valid after its
  certificate has expired.** This is the first measurement of a SecretPrinter
  signature after its certificate's last day. That these files carry timestamps
  was read on 2026-10-04; the document's command does not show it, and it was
  not read again in this walk.
- **The certificate's life is 72 hours to the second,** which is the figure in
  Microsoft's documentation quoted in the 2026-10-04 finding. It began on
  2026-10-03; the workflow run that used it was on 2026-10-04.

## Making the configuration

Each part is the document's step, in the order they were used.

**1. The example.** Run in the unpacked folder, exactly as the document printed
it:

```powershell
SecretPrinter.Service.exe --print-example-config > secretprinter.json
```

It failed. PowerShell reported `CommandNotFoundException`: "The term
'SecretPrinter.Service.exe' is not recognized as the name of a cmdlet, function,
script file, or operable program", and suggested `.\SecretPrinter.Service.exe`.
No file was made. With `.\` in front:

- exit code 0, and a file of 20 lines, the count the 2026-10-04 finding
  records for the example;
- the file's first two bytes were 255 and 254. That is the mark of UTF-16,
  which is the form Windows PowerShell's `>` writes.

**2. The adapters.** The two commands under "Configuring" listed `Ethernet` and
`Wi-Fi` as up, with the two virtual adapters, and gave the addresses above.

**3. The probe.** The signed `SecretPrinter.Probe.exe`, with `--interface`
naming the printer-side address:

- exit code 0, and 131 lines, the count of 2026-10-04;
- round 1: 1,373 bytes from the printer, 5 answers and 11 additionals, naming 4
  instances; round 2: 1,305 bytes, 7 answers and 4 additionals;
- its summary: 2 replies, 16 records, 5 service types seen;
- five `INSTANCE` lines under five `SERVICE TYPE` headings:

  | `SERVICE TYPE` | `INSTANCE` |
  | --- | --- |
  | `_ipp._tcp.local` | `EPSON ET-3760 Series._ipp._tcp.local` |
  | `_ipps._tcp.local` | `EPSON ET-3760 Series._ipps._tcp.local` |
  | `_pdl-datastream._tcp.local` | `EPSON ET-3760 Series._pdl-datastream._tcp.local` |
  | `_printer._tcp.local` | `EPSON ET-3760 Series._printer._tcp.local` |
  | `_universal._sub._ipp._tcp.local` | `EPSON ET-3760 Series._ipp._tcp.local` |

  So the name ending `._ipp._tcp.local` is printed twice. The document said to
  take the line ending `._ipp._tcp.local` and did not say there are two.

**4. The UUID.** `[guid]::NewGuid()` printed one value.

**5. The certificate.** The document's one-line command, run in Windows
PowerShell on the test machine, with the two differences in the table above. It
printed four lines:

- `SHA256` and `SHA1` values equal, character for character, to the ones
  recorded in
  [the finding of 2026-09-15](2026-09-15-printer-requires-tls-for-job-operations.md);
- the subject, `O=SEIKO EPSON CORP., CN=EPSON000000`;
- `Tls12`.

This is the first time that command has been run on Windows 11. The document
did not say where the printer's address and host name are to be found, and it
puts this step before the step that runs the probe, whose output holds both.

**6. The file.** Edited in Notepad. Three lines were changed: `clientInterfaces`
to `[ "Ethernet" ]`, `printerCertificateSha256` to the 64 digits of part 5, and
`uuid` to the value of part 4.

- **The other three values were already right.** `printerInterface` and the two
  instance names in the example matched what was measured, because the example
  was written from this printer and the test machine's printer-side adapter is
  named `Wi-Fi`, as the example's is. Another installation changes more.
  `instanceName`, `hostLabel`, `port` and the three tuning values were left as
  the example has them.
- **As saved, the file had 18 lines,** not 20: the example's two blank lines
  were gone. Its first two bytes were still 255 and 254.

## The run

FIOS-STB-01's service was stopped first, so that one machine offered the
printer. The two firewall rules were read back before the start: both enabled,
`Private`, naming the signed `SecretPrinter.Service.exe`, on 5353 and 631.

```powershell
.\SecretPrinter.Service.exe --config secretprinter.json --log-file <walk folder>\sp-walk.log
```

It ran as the signed-in user. Edwin was asked to use a window that was not
elevated; whether it was is not recorded. From its log:

- **04:22:34Z** start. **The configuration was accepted.** The start-up block
  shows `Ethernet` at 192.168.1.207, `Wi-Fi` at 192.168.12.142, both instance
  names, the certificate pin and the UUID, each as typed into the file. The
  resolver socket joined 224.0.0.251 on `Wi-Fi` only; the responder sockets
  joined both groups on `Ethernet`. The printer answered in the same second.
- **04:22:35Z** `Probing for the advertised names`. **04:22:36Z** `Probe
  clear`. **04:22:38Z** `Announced`, and two listeners: on 192.168.1.207:631,
  and on the client interface's link-local address, port 631.
- **04:23:12Z** the first connection. Eleven were accepted, all from one
  link-local client address, and all eleven were relayed with `encryption to
  printer: TLS 1.2, certificate matched the pinned fingerprint`.
- **The page printed.** Edwin noted tapping Print at 12:23 am Eastern, which is
  04:23Z, and that the page started printing right away and came out well.
- **04:24:22Z** Ctrl+C: `No longer accepting print jobs`, `Advertisement
  retracted (goodbye records sent)`, `Served 10 quer(ies) of 19 seen; 9 were
  for other services and were ignored`, `IPv4 answered 0 of 1 seen; IPv6
  answered 10 of 18 seen`, `SecretPrinter stopped`.

The eleven connections:

| Accepted | Count | Ended | Bytes to the printer, and back |
| --- | --- | --- | --- |
| 04:23:12Z to 04:23:13Z | 4 | 04:23:18Z | 1,445 and 2,644 on the first; 657 and 498 on each of the other three |
| 04:23:26Z | 4 | 04:23:57Z to 04:24:00Z | 9,198 and 6,972; 2,759 and 3,640 on two; 657 and 498 |
| 04:23:44Z | 1 | no ending line | not logged |
| 04:23:49Z | 1 | 04:24:21Z | 1,825 and 913 |
| 04:24:00Z | 1 | no ending line | not logged |

FIOS-STB-01's service was started again afterwards and was accepting at
04:27:17Z.

## What this shows

- **A configuration made by hand from `operating.md` is accepted by the
  service, and prints.** Every value in it came from a step in that document:
  the printed example, the two adapter commands, the probe, the UUID command
  and the certificate command.
- **A build signed by the release workflow runs,** on a machine with no .NET
  SDK and no .NET runtime: the service from a console window, and the probe.
- **The two checks in "Checking a release before you install it" pass on a
  second machine,** and the signature on the service still read `Valid` after
  its certificate had expired.
- **The service reads a configuration file written as UTF-16,** which is what
  Windows PowerShell's `>` produces, and Notepad on this machine kept that form
  when the file was edited and saved.
- **The certificate command works on Windows 11** and gave the fingerprints
  measured on Windows 10 three weeks earlier.

## The three faults in the instructions

All three are corrected in `operating.md` in the commit that carries this
finding.

1. **Three commands did not run as printed.** The command under "Configuring"
   and the two under "Running" named `SecretPrinter.Service.exe` with no `.\`
   in front. PowerShell does not run a program from the current folder by its
   bare name. The first of the three was run as printed and failed; the other
   two are written the same way and were not run as printed. All three have
   been in the document since its first version, `699598b`, of 2026-09-05. The
   probe's command, added on 2026-10-04, has the `.\`.
2. **The certificate step did not say where its two values come from,** and
   came before the step whose output holds them.
3. **The document did not say that the `._ipp._tcp.local` name is printed
   twice.**

## What this does not show

- **No release exists.** The build is the trial `0.1.0-rc.2`.
- **A person working from the document alone.** See "What 'following the
  instructions' means here".
- **Installing the signed files as a Windows service.** The run was from a
  console window. Starting at boot, updating and uninstalling were not
  exercised either.
- **A file downloaded by a browser.** Windows marks such a file, and what it
  then shows when the zip is unpacked or a program in it is first run was not
  seen. The files here were copied from another machine.
- **A firewall with no rules.** The rules existed already and were repointed.
  The document's `New-NetFirewallRule` commands were not run in this walk.
- **More than one page, one machine and one printer,** for 1 minute 48 seconds.
- **The probe's `ADDRESS` line.** The corrected document tells the reader to
  take the printer's address from it. That is read from the probe's code. In
  the walk the address was typed in by Claude, who had it from the probe's
  progress lines and from earlier findings; the `ADDRESS` line was not looked
  at.
- **The example's own UUID.** A configuration that keeps the example's fixed
  UUID loads, as
  [the finding of 2026-09-15](2026-09-15-example-config-leaves-fingerprint-empty.md)
  records. It was replaced here, so that case was not met.
- **Which .NET runtime the signed build carries.** Still not read.
- **Whether the signatures on the other 201 files were read after the
  certificate expired,** by the machine's own clock.

## Also seen, not explained

- **Two connections have no ending line.** They were accepted at 04:23:44Z and
  04:24:00Z and were open when the stop came at 04:24:22Z. The same was seen in
  both runs of 2026-10-04 and before; whether `REQ-OBS-006` covers it is still
  open.
- **The log does not show which connection carried the page.** The largest of
  the nine connections with an ending line carried 9,198 bytes to the printer.
  In the two runs of 2026-10-04 the largest carried 64,180 and 159,691. The two
  connections with no ending line have no byte counts.
- **Almost no IPv4 questions were seen:** 1, against 18 over IPv6, in 1 minute
  48 seconds. The console run on this machine on 2026-10-04 saw 771 over IPv4
  and 20 over IPv6 in 2 minutes 20 seconds. The firewall rule for UDP 5353 was
  read back before the run and named the program that ran.
- **The saved file lost the example's two blank lines.** What removed them is
  not established. They carry no meaning.

## What Claude predicted wrongly

- **That the saved file would have 20 lines.** It had 18.
- **That the probe would print four `INSTANCE` lines.** It printed five. The
  finding of 2026-10-04 lists the four services and does not say a name is
  repeated.
- **That the certificate had "probably not expired yet".** It had, 72 minutes
  before the reading. Claude worked that out from the date of the workflow run
  and did not know the certificate had begun the day before.
- **Which four lines FIOS-STB-01's log would end with after its restart.** A
  client connected one second after the service began accepting, so the last
  lines were that connection's.
- **Twice, a filter Claude wrote matched more lines than it was meant to,** and
  the extra lines were shown: the probe's progress lines, and its summary.

## Left on the test machine

`<walk folder>`, holding the zip, its `.sha256` file, the unpacked folder with
`secretprinter.json` in it, and `sp-walk.log`. The two firewall rules name the
program in that folder. The unsigned service installed on 2026-10-04 is
untouched and stopped.
