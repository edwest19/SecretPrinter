# A release folder printed on a machine with no .NET

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-04. Reviewed by a human before merge.*

**Status: the first sentence of `REQ-DIST-011` is measured. A folder made by
`publish-release.ps1` at `26ddfbe` was unpacked on a second machine that had no
.NET SDK and no .NET runtime installed. SecretPrinter ran from it twice on
2026-10-04, once in a console window and once installed as a Windows service
under `LocalService` by the steps in [`operating.md`](../operating.md), and an
iPhone printed a page through it each time. The probe, from the same folder,
listed the printer's instances. That machine runs Windows 11, so these are also
the first runs of SecretPrinter on Windows 11. No release exists yet, and what
these runs do not show is listed below.**

No IPv6 address, MAC address or device name appears in this finding, and the
second machine is not named. They are household values. It is called the test
machine here.

## How the folder was made

On the development machine, from a clean working tree at `26ddfbe`:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-release.ps1 -OutputDirectory <a new folder outside the repository>
```

- Both publishes built with no warnings.
- The script's own checks passed: `SecretPrinter.Service.exe` and
  `SecretPrinter.Probe.exe` exist, and each one's `runtimeconfig.json` lists an
  included runtime, `Microsoft.NETCore.App 10.0.11`.
- **216 files, 82,222,884 bytes.**
- With the publish's `win-x64` build output left under `bin`, the full tests
  and SpecCheck were unchanged: 411 passed, 23 assemblies, 416 test records,
  4 binding gaps, 0 stale assemblies.

The folder was packed with `Compress-Archive` into one zip of 36,155,646
bytes, SHA-256
`6B935740629DBA40E3B23E1B87D6B404C8CCBEF05D8C5118A47C845D430E74EE`, and copied
to the test machine, where the hash was the same.

## The test machine

- **Windows 11 Home, build 26200,** 64-bit.
- **Two networks, as on FIOS-STB-01:** `Ethernet` at 192.168.1.207 on the
  client side and `Wi-Fi` at 192.168.12.142 on the printer's side. Windows
  classed both as `Private`.
- **It had .NET, and it was removed for this test.** That is not the same as a
  machine that never had it, and this finding does not claim that.
  - Before: SDK 10.0.301; runtimes `Microsoft.NETCore.App` 10.0.9 and 10.0.11,
    `Microsoft.AspNetCore.App` 10.0.9 and `Microsoft.WindowsDesktop.App` 10.0.9.
    No installed program was listed for the 10.0.11 runtime.
  - The SDK was uninstalled with its own installer
    (`dotnet-sdk-10.0.301-win-x64.exe /uninstall`). That removed every .NET 10
    entry from the installed programs.
  - It left `C:\Program Files\dotnet` holding `host\fxr\10.0.11` (1 file) and
    `shared\Microsoft.NETCore.App\10.0.11` (189 files). That folder was deleted
    by hand from an elevated window.
- **Checked afterwards, before anything was run:**
  - `dotnet` was not found on the path;
  - no `coreclr.dll` under `C:\Program Files\dotnet`,
    `C:\Program Files (x86)\dotnet` or `%LOCALAPPDATA%\Microsoft\dotnet`;
  - `DOTNET_ROOT` was empty;
  - the only installed programs with `.NET` in their names were
    `Microsoft .NET Framework 4.8.1 SDK` and its Targeting Pack. The .NET
    Framework is a different product, and SecretPrinter does not use it.

## What was run with no .NET on the machine

| Run | Result |
| --- | --- |
| Unpack the zip | 216 files |
| `SecretPrinter.Service.exe --help` | usage text, exit code 0 |
| `SecretPrinter.Service.exe --print-example-config` | 20 lines, exit code 0 |
| `SecretPrinter.Probe.exe --help` | usage text, exit code 0 |
| The service, given its own example configuration | `Configuration is not usable, so the service will not start.`, 3 lines, exit code 3 |
| The probe on the adapter holding the default route, `--timeout 3` | 26 lines, exit code 1: no reply was received. Which adapter that was is not recorded |
| The service in a console window | an iPhone printed a page; see below |
| The service as a Windows service under `LocalService` | an iPhone printed a page; see below |
| The probe on the printer's side, from the installed folder | "Round 1 named 4 instance(s)"; `INSTANCE` lines for the printer's `_ipp`, `_ipps`, `_pdl-datastream` and `_printer` services; 131 lines; exit code 0 |

## The console run

- **Configuration:** FIOS-STB-01's file, unchanged. It names adapters, and the
  test machine's two adapters have the same names.
- **Firewall:** the two rules [`operating.md`](../operating.md) gives, inbound
  UDP 5353 and inbound TCP 631, `Private`, naming the program in the unpacked
  folder.
- **FIOS-STB-01's service was stopped first,** so that only one machine offered
  the printer under the advertised name.
- **It ran as the signed-in user,** not as a service.

From its log, all times UTC:

- **18:41:34Z** start. The resolver socket bound and joined 224.0.0.251 on
  `Wi-Fi` only. The responder sockets bound for IPv4 and IPv6 and joined both
  groups on `Ethernet`. The printer answered for its capabilities and for where
  to connect in the same second.
- **18:41:35Z** `Probe clear`. **18:41:37Z** `Announced`, and two listeners: on
  192.168.1.207:631, and on the client interface's link-local address, port 631.
- **18:42:21Z** the first connection, from a link-local client address. 14
  connections were accepted in all. Every one was relayed with
  `encryption to printer: TLS 1.2, certificate matched the pinned fingerprint`.
  The largest carried 64,180 bytes to the printer and 7,562 back, in 36.49 s.
- **The page printed.** The time Print was tapped was not noted.
- **18:43:54Z** Ctrl+C: `No longer accepting print jobs`,
  `Advertisement retracted (goodbye records sent)`,
  `Served 24 quer(ies) of 791 seen`, `IPv4 answered 14 of 771 seen; IPv6
  answered 10 of 20 seen`, `SecretPrinter stopped`.

## The service run

The steps were those under "As a Windows service" in
[`operating.md`](../operating.md), with two differences:

- **The release folder was copied** to `C:\Program Files\SecretPrinter`, where
  the instructions then said to run `dotnet publish`. 216 files arrived.
- **`start= demand`** where the instructions say `start= auto`, so that the
  test machine never starts SecretPrinter by itself while FIOS-STB-01 is the
  household's proxy. Starting at boot was therefore not exercised.

What each step printed:

- `icacls` on `C:\ProgramData\SecretPrinter`: `Successfully processed 1 files;
  Failed processing 0 files`.
- `sc.exe --% create …`: `[SC] CreateService SUCCESS`.
- `sc.exe qc SecretPrinter`: `START_TYPE : 3 DEMAND_START`; the program path in
  quotes followed by `--config`, `--log-file` and `--service`;
  `SERVICE_START_NAME : NT AUTHORITY\LocalService`.
- The two firewall rules, repointed with `Set-NetFirewallRule`: both enabled,
  `Private`, naming `C:\Program Files\SecretPrinter\SecretPrinter.Service.exe`,
  on 5353 and 631.
- `sc.exe start SecretPrinter`: `START_PENDING`, and fifteen seconds later
  `STATE : 4 RUNNING`.

From its log, all times UTC:

- **19:00:36Z** `Service 'SecretPrinter' starting under the service control
  manager.` The same start-up as the console run followed.
- **19:00:37Z** `Probe clear`. **19:00:39Z** `Announced`, and the same two
  listeners.
- **19:02:19Z** the first connection, from a link-local client address
  different from the one in the console run. Whether it was the same device is
  not established. Six connections were accepted, every one relayed with TLS
  1.2 and the pinned certificate matched. The largest carried 159,691 bytes to
  the printer and 6,171 back, in 57.44 s.
- **The page printed.** Edwin noted tapping Print at 3:00 pm Eastern, which is
  19:00Z, and that the page came out right away. The log's first connection is
  two minutes and nineteen seconds after 19:00:00Z. Both are recorded as they
  are.
- **19:03:36Z** `Stop-Service`: `Service 'SecretPrinter' stopping.`, the
  listeners closed, the goodbye sent, `Served 34 quer(ies) of 761 seen`,
  `IPv4 answered 23 of 733 seen; IPv6 answered 11 of 28 seen`,
  `SecretPrinter stopped`.

FIOS-STB-01's service was started again after each run. It announced at
18:47:11Z and at 19:07:29Z.

## What this shows

- **A folder made by `publish-release.ps1` runs on a machine with no .NET SDK
  and no .NET runtime installed,** as a console program and as a service under
  `LocalService`, and prints.
- **The service install steps in `operating.md` work with a copied release
  folder** in place of a build from source.
- **The probe does its job from that folder:** it found the printer's instance
  names, which is what the configuration step uses it for.
- **On Windows 11 Home, build 26200,** the things first measured on Windows 10
  held for these two short runs: sharing UDP 5353, joining the multicast groups
  for both address families, listening on a link-local address with its scope,
  TLS 1.2 to the printer with the pinned certificate, and running as
  `LocalService`.

## What this does not show

- **No release exists.** The folder was built on the development machine by
  hand. No release workflow built it, nothing in it is signed by this project,
  and no tag or changelog entry stands behind it. `REQ-DIST-004`, `005` and
  `006` are untouched.
- **Two runs of about two and three minutes, one page each, one test machine.**
  Nothing here says how SecretPrinter behaves on Windows 11 over hours. The
  losses of the printer's side seen on FIOS-STB-01 were not looked for.
- **Starting at boot, updating an installed release, and uninstalling** were
  not exercised.
- **The configuration tool does not exist yet.** `REQ-DIST-011` covers every
  tool the operating documentation tells someone using a release to run. Today
  that is the probe. When the configuration tool is built and the documentation
  tells people to run it, it has to be added to `publish-release.ps1`.
  *(Status note, 2026-10-05, by Claude, Claude Opus 5.5: on 2026-10-05 Edwin
  decided that release 0.1.0 has no configuration tool and that this repository
  will not have one. A release is configured by hand, with the steps in
  [`operating.md`](../operating.md); see README open question 9. So the probe
  stays the only tool the documentation tells someone using a release to run,
  and nothing has to be added to `publish-release.ps1`. Those steps have not
  yet been followed from the first to the last on a release folder.)*
  *(Status note, 2026-10-06, by Claude, Claude Opus 5.5: they were followed on
  2026-10-06, on a folder the release workflow had signed, and a page printed;
  see [the finding](2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md).)*
- **Which .NET runtime a release carries depends on the machine that builds
  it.** The development machine's build carried 10.0.11. A trial on 2026-10-03
  on the test machine itself, while it still had SDK 10.0.301, produced a
  folder of 216 files too (82,108,378 bytes), carrying a `coreclr.dll` that reports
  `10,0,926,27113`, which Claude reads as 10.0.9. Microsoft's documentation
  says a self-contained publish takes the runtime from the SDK on the
  publishing machine, and that the application must be published again to
  obtain a newer patch
  ([runtime patch selection](https://learn.microsoft.com/dotnet/core/deploying/runtime-patch-selection)).
  So a release has to state the runtime it carries, and fixes to that runtime
  reach an installed copy only through a new release. Neither is written into
  the README yet.
- **Whether the shipped files are the tested files.** Publishing for `win-x64`
  builds the service's and the probe's own assemblies again, in another folder.
  Whether those come out byte for byte the same as the ones the tests ran
  against was not measured.

## Also measured, for the signing step

On the 2026-10-03 trial folder, service only, 211 files,
`Get-AuthenticodeSignature` reported:

| Files | Signature |
| --- | --- |
| 184 `.dll`, 1 `.exe` | valid, signer `CN=.NET, O=Microsoft Corporation` |
| 3 `.dll` | valid, signer `CN=.NET DAC, O=Microsoft Corporation` |
| 2 `.dll` | valid, signer `CN=Microsoft Corporation` |
| 9 `.dll`, 1 `.exe` | not signed |
| 9 `.pdb`, 2 `.json` | cannot carry a signature |

The ten unsigned files match, by count, the nine SecretPrinter assemblies and
`SecretPrinter.Service.exe`; they were not listed by name. The folder with the
probe in it was not examined this way.

## Also seen, not explained

- **In each run, one connection has no ending line.** In the console run, 13
  of the 14 accepted connections have a `completed` line; the last, accepted at
  18:43:52Z and relaying at 18:43:53Z, has none before the stop at 18:43:54Z.
  In the service run, five of six have one; the first, accepted at 19:02:19Z,
  has none before the stop at 19:03:36Z. Both were open when the stop came. The
  same was recorded on 2026-09-25 and as item 6 of
  [the 2026-10-02 finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md),
  where the question was whether `REQ-OBS-006` covers it. Still open.
- **The first probe run received no reply.** It asked on the adapter holding
  the default route. Which network that was, and why nothing answered there,
  are not established.

## Three things Claude got wrong on the way

- **The uninstall instructions named programs approximately.** Nothing was
  removed until the SDK's exact uninstall command was read from the registry.
- **Claude read a PowerShell prompt as a sign the window was not elevated.** A
  window with the same prompt then deleted a folder under `C:\Program Files`.
  The prompt says nothing about elevation.
- **Claude said the configuration would need two addresses changed.** The
  configuration names adapters, and needed no change.
