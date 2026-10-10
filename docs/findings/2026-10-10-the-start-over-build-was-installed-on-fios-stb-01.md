# The start-over build was installed on FIOS-STB-01

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-10. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-10 a build of commit `5db9063`, which
starts the questions to an unreachable printer over when the printer-side
adapter is usable again (README `REQ-RES-010`;
[finding](2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md)),
was made on FIOS-STB-01 by `publish-release.ps1` and installed there in place
of release 0.1.0, to run overnight before a release. It probed, announced and
accepted print jobs, and an iPhone printed a page through it. The household had
no printer for 7 seconds. The first build carried .NET runtime 10.0.9, three
security releases behind 0.1.0's 10.0.12, and was not installed: the .NET SDK
on FIOS-STB-01 was updated to 10.0.401 first, which took two tries, and the
build was made again with 10.0.12. The start over itself has not yet happened
on a network: the printer was not lost between the install and 22:00Z, when
the log was last read. No code changed.**

No IPv6 address, hardware address, network name, user name or device name
appears in this finding, and neither does the printer's host name. Folders in
Edwin's user folder on FIOS-STB-01 are written `<user folder>`.

**About the times.** Times ending in `Z` are UTC, from the service's log or
converted from the times Windows and the installer logged, which are US
Eastern. Times in pm are US Eastern, by Edwin West's account or his iPhone's
clock. Everything here happened on 2026-10-10.

## Why this was done

On 2026-10-10 Claude asked Edwin where a build of `5db9063` should run for its
first run on hardware, recommending a console window on the test machine, as
for [the run of 1 pm that day](2026-10-10-questions-carrying-their-answers-went-unanswered-on-a-network.md),
with FIOS-STB-01 stopped and not changed. The other two courses were a console
on FIOS-STB-01, or installing the build there and putting 0.1.0 back afterwards.
Edwin chose to install it on FIOS-STB-01 and not go back: "lets run the new
build on fios-stb-01 server in place of what is there and we will run it
overnight", "we wont go back we will just collect data and run it for a soak".
He plans a release the next day. No drop of the Wi-Fi on purpose is planned;
the drops that come by themselves are the data.

How the build would reach FIOS-STB-01 was a second question. Claude recommended
building it there, from FIOS-STB-01's own clone, with `publish-release.ps1`,
which is the script the release workflow runs, and installing it by the steps
of [`operating.md`](../operating.md) under "Updating an installed service",
"From a release". That gives the release's shape, self-contained, with no file
moved between machines. The other courses were to build on the development
machine and copy the zip over, or the "From source" steps as printed, which
publish a framework-dependent build over the program folder and would leave
0.1.0's files beside it. Edwin chose the first.

`operating.md` does not describe this course. It is the "From a release" steps
with a folder that was built on the machine and is not signed.

## The clone

FIOS-STB-01's clone, in `<user folder>`, was on `main`, with nothing changed,
at `64e8fc2` of 2026-10-01. `git pull --ff-only` moved it to `5db9063`: 25
commits, 88 files changed, 10,484 insertions and 1,677 deletions. Its last
commit then read `5db9063ca3d6e74f94d84073f8966ad361228699`, with parent
`0e04d05`, the commit on GitHub.

## The first build carried an older runtime, and was not installed

`publish-release.ps1`, run in the clone into a new folder in `<user folder>`,
reported 216 files, 82,125,919 bytes, the service and the probe each carrying
`Microsoft.NETCore.App 10.0.9`, and the stamp
`0.1.0+5db9063ca3d6e74f94d84073f8966ad361228699`.

Release 0.1.0, which was running, carries 10.0.12. The only SDK on FIOS-STB-01
was 10.0.301. Windows recorded it, with runtime 10.0.9, as installed on
2026-07-02 by Microsoft's installer.

Microsoft's release metadata for .NET 10, read that day at
`builds.dotnet.microsoft.com`, gave 10.0.12, of 2026-09-08, as the latest
release, and 10.0.401 as the latest SDK. It marks 10.0.10 (2026-07-14), 10.0.11
(2026-08-11) and 10.0.12 (2026-09-08) as security releases, listing 17, 10 and
6 CVEs. Which of them touch what SecretPrinter uses was not read. The SDKs it
lists for 10.0.12 are 10.0.401 and 10.0.112; none is 10.0.3xx.

So the build would have put the household's proxy, which faces the client
network, three security releases behind what it was running. Claude put that
to Edwin, recommending that the SDK be updated and the build made again. Edwin
chose that.

The three builds of the day show the pattern. A self-contained build carried
the runtime that the SDK building it knew: 10.0.303 put in 10.0.11 on the
development machine earlier that day, 10.0.301 put in 10.0.9 here, and 10.0.401
put in 10.0.12. Neither `publish-release.ps1` nor `operating.md` says so.

## Updating the SDK

**The installer.** A command on FIOS-STB-01 read Microsoft's release metadata,
took from it the address and SHA-512 hash of the 10.0.401 installer for
`win-x64`, and downloaded it. The file was
`dotnet-sdk-10.0.401-win-x64.exe`, 215,437,248 bytes. Its SHA-512 matched the
one Microsoft lists, and its Authenticode signature was `Valid`, signed
`CN=.NET, O=Microsoft Corporation`. Microsoft's page "Install .NET on Windows"
gives `/install /quiet /norestart` for a silent install, and `/repair` for a
version already installed.

**The first try failed.** Run with `/install /quiet /norestart` in Edwin's SSH
session, which runs elevated (`High Mandatory Level`), it ended at 21:37:47Z
with exit code 232 (`0xe8`). Its logs say:

- At 21:37:44Z the package that installs the .NET host failed with
  `0x80070641`, Windows error 1601, "The Windows Installer Service could not be
  accessed". Twice before that, Windows Installer's Restart Manager logged
  "Failed to shut down all applications in the service's session. Error: 351".
- On the retry, at 21:37:47Z, it logged that the package "has 16 applications
  holding files in use". In the same second the installer's main process lost
  its pipe to its per-machine process (`0x800700e8`) and gave up, so nothing it
  had put in was taken out again.
- The host package's own log says it was installed at 21:38:21Z, after the main
  process had stopped.

It left Windows recording the SDK 10.0.401 entry and the .NET Host and Host FX
Resolver 10.0.12, with no SDK 10.0.401 and no runtime 10.0.12 that `dotnet`
could list. No .NET file was queued for a restart.

Seven `dotnet.exe` processes were running from the .NET folder at 21:42Z, all
started at 21:27:03Z, when the first build ran. By 21:45Z none was running.
`dotnet build-server shutdown`, run then, reported the MSBuild and C# compiler
servers shut down. Whether they were what held files at 21:37Z, and why the
pipe broke, is not established.

**The repair worked.** Run again with `/repair /quiet /norestart`, with no
build process running, the installer ended with exit code 0. `dotnet` then
listed SDKs 10.0.301 and 10.0.401, and runtimes 10.0.9 and 10.0.12. The
installer did not remove 10.0.301 or 10.0.9.

## The build that was installed

The first build's folder was removed. In the clone, `dotnet --version` gave
10.0.401, which `global.json` selects. `publish-release.ps1` into a new folder
of the same name reported 216 files, 82,423,767 bytes, the service and the
probe each carrying `Microsoft.NETCore.App 10.0.12`, and the stamp
`0.1.0+5db9063ca3d6e74f94d84073f8966ad361228699`. It says 0.1.0 because the
version has not been changed since that release; the commit after the `+` tells
them apart.

It is not the release build. It was not signed, it did not come from the
release workflow, and it was built on the machine that runs it.

## The install

One command, run as Administrator, did steps 2 to 5 of "From a release": stop
the service, remove the program folder, copy the new folder in, read the stamp
and refuse to start unless it was `5db9063`'s, and start the service. Step 4's
signature check was left out, because nothing in the folder is signed. The
service's registration, configuration, log and firewall rules were not touched.

| Time | What the service logged |
| --- | --- |
| 21:52:55Z | 0.1.0: stopping; no longer accepting print jobs; goodbye records sent; its stop summary; `SecretPrinter stopped.` |
| 21:52:58Z | `Service 'SecretPrinter' starting under the service control manager.` |
| 21:52:59Z | Capabilities read from the printer; probing for the advertised names |
| 21:53:00Z | `Probe clear` |
| 21:53:02Z | `Announced. Answering queries.`; accepting print jobs on 192.168.1.161:631 and on the client interface's link-local address |

The command reported 216 files in the program folder and the stamp above. No
warning or error was logged.

**The household had no printer for 7 seconds,** from 0.1.0's stop at 21:52:55Z
to the first `Accepting print jobs` line at 21:53:02Z.

0.1.0's stop summary covers its run since it started at 17:22:21Z, after the
run on the test machine at 1 pm:

```
Served 64 quer(ies) of 1966 seen; 1902 were for other services and were ignored.
By transport: IPv4 answered 21 of 1209 seen; IPv6 answered 43 of 757 seen.
```

64 and 1,902 add up to 1,966. That is 0.1.0's form of the line, which has no
count of questions that carried their answers (`REQ-ADV-026`). Release 0.1.0
ran on FIOS-STB-01 from 2026-10-09 17:39:26Z to 2026-10-10 21:52:55Z, stopped
once in between for the run on the test machine, from 17:09:51Z to 17:22:21Z.

## The print

Edwin tapped Print at 5:57 pm and the page printed: "printing started
immediately". In his screenshot at 5:57 pm the iPhone's list held
`SecretPrinter (ET-3760)` and `EPSON ET-3760 Series`, both under Known
Printers. The proxy's note read "SecretPrinter proxy. Capabilities read from
the printer at 2026-10-10 21:52:59Z", the new build's start. Both rows were
under Known Printers, as on 2026-10-09 through FIOS-STB-01's installation,
whose configuration and UUID were kept; through the test machine's, in the run
at 1 pm and on 2026-10-08, the proxy was under Other Printers.
[`operating.md`](../operating.md) records this under "What you will see on an
iPhone". Why it differs is not established. Claude first told Edwin the proxy
had moved, without having reread that section.

In his screenshot of the print options at 5:57 pm the printer chosen read
`EPSON ET-3760 Series`, the printer's own name. That is the known problem the
README states, "An iPhone that has printed to the printer directly may show
the printer's own name". The service's log shows the page went through the
proxy:

- **8 connections** after the start, the first opened at about 21:56:47Z by its
  duration. All came from the iPhone's link-local address, all were relayed
  with the printer's certificate matching the pinned fingerprint, and all 8
  completed. None failed and none was refused.
- **The page:** one connection carried 1,061,107 bytes to the printer and 3,924
  back in 59.03 seconds, ending at 21:58:47Z, so it opened at about 21:57:48Z,
  when Edwin tapped Print. The others carried between 1,314 and 88,695 bytes.

## What Claude predicted, and what it did not

Every prediction about the commands held: the clone's state, the pull's
counts, the build's file count and stamp, the installer's hash and signature,
the repair's result, and the install's lines and timing. The first build's
runtime, and the size of each folder, were said in advance to be unknown.

Not predicted:

- **The first try of the SDK installer failing,** with exit code 232. The
  prediction was 0 or 3010.
- **The signer's name.** Claude predicted `CN=Microsoft Corporation`; the
  certificate reads `CN=.NET, O=Microsoft Corporation`.
- **Lines kept by the filters that were not wanted.**
  PowerShell's `-match` and `Select-String` ignore case: `NetCore.App` matched
  `AspNetCore.App`, `Announced` matched "not announced", `reachable` matched
  "unreachable", and `Served` matched "observed". Nothing was hidden.
- **The seven build processes being gone** before the command meant to stop
  them ran.

## What this does not show

- **The start over on a network.** The printer was not lost between 21:53:02Z
  and 22:00Z, so `REQ-RES-010` has not acted. The overnight run is for
  that, and it will be read the next day.
- **The release build,** which the release workflow builds and signs.
- **Why the first install failed.**
- **Which of the 33 CVEs touch SecretPrinter.**

## Left on FIOS-STB-01

All Edwin's to keep or remove:

- The installed build, and the folder it was copied from, in `<user folder>`.
- The clone, now at `5db9063`.
- .NET SDKs 10.0.301 and 10.0.401, and runtimes 10.0.9 and 10.0.12. Runtimes
  8.0.10, of 2025-05-19, and 6.0.14, of 2023-06-27, were already installed.
  Nothing of SecretPrinter's uses any of them.
- In the account's Downloads folder: the SDK installer, Microsoft's release
  metadata file, and release 0.1.0's zip and `.sha256`. The zip is the way back
  to 0.1.0, by the same steps; Edwin chose not to go back.
- The installer's logs in the account's temporary folder.
