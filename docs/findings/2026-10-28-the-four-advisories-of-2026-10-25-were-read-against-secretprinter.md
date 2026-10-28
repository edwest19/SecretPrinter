# The four advisories of 2026-10-25 were read against SecretPrinter

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-28. Reviewed by a human before merge.*

**Status: worked out from Microsoft's own advisories, from SecretPrinter's
assemblies as they ship in releases `0.1.0`, `0.4.0` and `0.5.0`, from the .NET
runtime those releases carry, and from the runtime's public source. By what
Microsoft says each fault needs, SecretPrinter reaches none of the four.
Releases `0.1.0` to `0.4.0` carry a .NET runtime that Microsoft lists as
affected by two of them, CVE-2026-111497 (Brotli) and CVE-2026-70197 (sockets);
`0.5.0` carries the corrected runtime, 10.0.14. Nothing in SecretPrinter is
changed. No attack on any of the four was tried.**

| Advisory | What Microsoft says it needs | SecretPrinter |
| --- | --- | --- |
| CVE-2026-111497, Brotli decoder | Brotli data decompressed by ASP.NET Core request decompression, `HttpClient` automatic decompression, or `BrotliStream` or `BrotliDecoder` | Decompresses nothing. No shipped assembly references `System.IO.Compression`, `System.Net.Http` or `Microsoft.AspNetCore`. |
| CVE-2026-114148, Linux archive ownership | An installation or build from an affected Linux tarball | Built and run on Windows only. |
| CVE-2026-70197, Windows sockets | One `SocketAsyncEventArgs` reused across an IPv4 and an IPv6 datagram socket | Never names that type. The receive calls it makes keep the one they use on the socket they are called on. |
| CVE-2026-102870, Blazor WebAssembly debugger | A reference to `Microsoft.AspNetCore.Components.WebAssembly.Server` or `.DevServer` | Its only packages are `System.ServiceProcess.ServiceController` and `System.Diagnostics.EventLog`. |

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name.

## Why this was done

[The finding of the fifth release](2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md)
left it open: "Whether the runtime's own code under those calls is what the
advisory describes is not worked out here. None of the other three advisories
was examined against SecretPrinter either." `SECURITY.md` said the same, and so
did a status note on
[the finding of the fourth release](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md).
The handoff of 2026-10-27 carried it among the items for Claude to settle;
Edwin West asked for it to be worked on that day.

## What Microsoft published

All read on 2026-10-28 in the browser pane of the Claude desktop app on the
development machine, read-only.

**The announcement,** dotnet/announcements issue 496: "On October 25, 2026, we
released out-of-band security updates for .NET 8.0.33, .NET 9.0.22, .NET
10.0.14, and .NET 11 Release Candidate 3 (prerelease)." Its table, column
"Applicability":

- CVE-2026-111497: "Brotli decoder out-of-bounds write; all platforms when
  processing untrusted Brotli data."
- CVE-2026-114148: "Linux archive ownership; distribution-specific ASP.NET Core
  archives and source-build engineering inputs."
- CVE-2026-70197: "Windows sockets memory corruption; corrected October 13
  fix."
- CVE-2026-102870: "Standalone Blazor WebAssembly debugger packages on .NET
  10/11; corrected October 13 fix."

Each advisory's own page, linked from that table, was read in full: GitHub
advisories GHSA-p3jp-9v83-76w9 (dotnet/runtime), GHSA-r6pf-x897-gm86
(dotnet/aspnetcore), GHSA-4mpw-cpwj-hxxj (dotnet/runtime) and
GHSA-g27p-qf6j-c96c (dotnet/aspnetcore). What each says is quoted below where
it is used.

## How SecretPrinter was examined

- **The releases as shipped.** The zips of `0.1.0`, `0.4.0` and `0.5.0` were
  downloaded from this repository's Releases page into Claude's workspace, each
  into an empty folder of its own, and each matched the SHA-256 published
  beside it. Nothing in them was run. Their `System.Net.Sockets.dll` name their
  runtimes as `10.0.12+95017c71…`, `10.0.13+4099f839…` and `10.0.14+0427b9ab…`,
  in each assembly's informational version. Release `0.4.0` was taken for
  `0.3.0` and `0.4.0`, which carry the same runtime, and `0.1.0` for `0.1.0`
  and `0.2.0`.
- **What SecretPrinter's code refers to.** A scratch tool read the
  type-reference and member-reference tables of the twelve assemblies a release
  ships beside the runtime: SecretPrinter's ten, and the two of its one
  package, `System.ServiceProcess.ServiceController` and
  `System.Diagnostics.EventLog`. In all three releases, and in a build of the
  code of `ad99298` in Claude's workspace, no assembly refers to a type in
  `System.IO.Compression`, `System.Net.Http`, `Microsoft.AspNetCore` or
  `Microsoft.Extensions.Http`, or to any type whose name contains `Brotli` or
  `SocketAsyncEventArgs`. The methods they call on `Socket`, `TcpListener` and
  `TcpClient` to receive or accept are `Socket.ReceiveMessageFromAsync`
  (`SecretPrinter.Mdns`), `Socket.ReceiveFromAsync` (`SecretPrinter.Probe`),
  `TcpListener.AcceptTcpClientAsync` and `TcpClient.ConnectAsync`
  (`SecretPrinter.Proxy`). This is the same method `SecurityClaimsTests` uses:
  a type an assembly does not refer to is one no code path in it can use.
- **What a release restores.** The development machine's restore record for the
  service, `obj/project.assets.json`, read through the link to that machine:
  two packages, `System.ServiceProcess.ServiceController` and
  `System.Diagnostics.EventLog`, both 10.0.11, and one framework,
  `Microsoft.NETCore.App`. The probe's: no package. Every release folder holds
  216 files; none has `AspNet`, `Blazor` or `WebAssembly` in its name.
- **What a release folder carries of the runtime.** All of it, as a
  self-contained folder must, so each holds `System.IO.Compression.Brotli.dll`,
  `System.IO.Compression.Native.dll` and `System.Net.Http.dll` among the
  runtime's files. Being present is not being used; see CVE-2026-111497.

## CVE-2026-111497: Brotli decoder

**What Microsoft says it needs.** "ASP.NET Core and HttpClient do not
automatically decompress Brotli data in their default configurations.
Applications are exposed when they enable Brotli decompression or explicitly
use Brotli decompression APIs to process attacker-controlled data". And: "An
application is not exposed merely because an affected .NET package is
installed. Exposure requires the application to decompress attacker-controlled
Brotli data through one of these paths": ASP.NET Core request decompression
with Brotli, `HttpClient` automatic response decompression that includes
Brotli, or `BrotliStream` or `BrotliDecoder` used directly. The affected
packages include `Microsoft.NETCore.App.Runtime.win-x64` below 10.0.14.

**SecretPrinter.** It refers to no type in `System.IO.Compression`,
`System.Net.Http` or `Microsoft.AspNetCore`, in any release examined, so it
takes none of the three paths. The relay copies the bytes of a print job
between the client and the printer without reading them; it decompresses
nothing. The service (`SecurityClaimsTests`) and the probe (`ProbeClaimsTests`,
`REQ-SEC-016`) are already held by tests to referring to no HTTP client.

**Verdict.** Not reached. Releases `0.1.0` to `0.4.0` carry the affected
decoder among the runtime's files; `0.5.0` carries the corrected one.

## CVE-2026-114148: Linux archive ownership

**What Microsoft says it needs.** "Incorrect ownership of runtime files
installed from affected Linux tarballs can allow local elevation of privilege."
Platforms: Linux. "For Microsoft-published .NET 8 and .NET 9 distributions,
only the ASP.NET Core runtime and targeting-pack tarballs are affected." For
.NET 10, only distributions built from source on Linux, among them
"Source-built .NET 10.0.1xx Linux distributions" from 10.0.100 to 10.0.113.

**SecretPrinter.** Its releases are Windows x64 folders, built by the release
workflow on GitHub's Windows machines (`runs-on: windows-latest`) with the SDK
`actions/setup-dotnet` installs from Microsoft. The development machine and
FIOS-STB-01 run Windows. Nothing of SecretPrinter is built from, installed from
or run on a Linux tarball.

One Linux .NET does touch the project: Claude's workspace uses Ubuntu's package
`dotnet-sdk-10.0`, version 10.0.113, which Ubuntu builds from source. That
version is in the advisory's range for source-built 10.0.1xx distributions. It
was installed by Ubuntu's package manager, not extracted from a tarball, and
whether Ubuntu's package carries the fault is not known. The workspace is a
temporary container; what is built there is used to run the tests before the
development machine runs them, and nothing built there is shipped or installed.

**Verdict.** Not reached.

## CVE-2026-70197: Windows sockets

**What Microsoft says it needs.** "On Windows, reusing a SocketAsyncEventArgs
instance across IPv4 and IPv6 datagram sockets can retain an undersized cached
native address buffer, allowing an unauthenticated out-of-bounds write during
ReceiveFromAsync." Its FAQ: "If using a package listed in affected packages,
you're exposed to the vulnerability", and "The issue requires reuse of
SocketAsyncEventArgs across IPv4 and IPv6 datagram sockets; it is not limited
to one runtime implementation." And: "This vulnerability was disclosed in the
October 13 advisory, but the fix was not included in the previous updates."
Affected: `Microsoft.NETCore.App.Runtime.win-x64` from 10.0.0 to below 10.0.14.
CVSS 3.1 score 8.1, High.

**SecretPrinter's code.** It refers to no `SocketAsyncEventArgs` in any release
examined, so it cannot create one or pass one to the runtime. The service
receives mDNS on two sockets, one IPv4 and one IPv6, each with its own buffer,
through `Socket.ReceiveMessageFromAsync(Memory<byte>, SocketFlags, EndPoint,
CancellationToken)` (`src/SecretPrinter.Mdns/MdnsSocket.cs`). The probe
receives on one IPv4 socket through `Socket.ReceiveFromAsync` with the same
four arguments. The relay accepts and makes TCP connections, which are not
datagram sockets.

**What the runtime does under those calls.** Those two methods do use a
`SocketAsyncEventArgs` inside the runtime. Read in the `System.Net.Sockets.dll`
of each of the three releases, with a scratch tool that lists fields and the
fields each method's IL uses, without loading the assembly:

- `Socket` holds the cached object in `_singleBufferReceiveEventArgs`, an
  instance field, not a static one: every socket has its own.
- Both four-argument `ReceiveFromAsync` methods and the four-argument
  `ReceiveMessageFromAsync` take the object from that field of the socket they
  are called on, or make a new one.
- The object, `Socket+AwaitableSocketAsyncEventArgs`, records the socket that
  made it in an instance field, `_owner`, set in its constructor. When an
  operation is done, `ReleaseForSyncCompletion` puts it back in `_owner`'s
  `_singleBufferReceiveEventArgs` (or `_singleBufferSendEventArgs`), and in no
  other socket's.

The same is in the runtime's public source at the tag `v10.0.13`
(`src/libraries/System.Net.Sockets/src/System/Net/Sockets/Socket.Tasks.cs`). So
under SecretPrinter's calls, an object used by the IPv4 socket stays with the
IPv4 socket, and one used by the IPv6 socket with the IPv6 socket: one is never
handed across, which is what the advisory says the fault needs.

**Where the fix is.** Between the `System.Net.Sockets.dll` of `0.4.0` (10.0.13)
and of `0.5.0` (10.0.14), three method bodies changed length:
`SocketAsyncEventArgs.AllocateSocketAddressBuffer` (69 to 99 bytes of IL),
`SocketAsyncEventArgs.FreePinHandles` (105 to 112), and a
`Socket.ReceiveFromAsync` with two parameters (254 to 276). By its size and the
fields it uses, that is the private overload taking a `SocketAsyncEventArgs`
and a cancellation token (`Socket.cs` in the public source). Four
compiler-generated methods were renumbered; 227 others differ in their bytes at
the same length, which is what metadata tokens moved by an added member look
like; they were not compared one by one. That is where the advisory's "cached
native address buffer" would be, and none of those methods is the per-socket
cache above. Between 10.0.12 (`0.1.0`) and 10.0.13 (`0.4.0`) two methods
changed, `Socket.Accept` and `SocketAsyncEventArgs.FinishOperationSyncSuccess`,
matching the public source's change at those tags in `Socket.cs` and
`SocketAsyncEventArgs.cs`, for an accept on macOS: none for this advisory, as
Microsoft's revision note says. The public source of the four files read is the
same at the tags `v10.0.13` and `v10.0.14`, so it shows neither fix, and the
public tags are not exactly what Microsoft shipped; the shipped assemblies are
what was read for the fix.

**Verdict.** Not reached, by the condition Microsoft's description and FAQ
give. In the FAQ's first sense, "If using a package listed in affected
packages, you're exposed", releases `0.1.0` to `0.4.0` are exposed: they carry
the runtime it lists. `0.5.0` carries the corrected one.

## CVE-2026-102870: Blazor WebAssembly debugger

**What Microsoft says it needs.** "You are affected if you use a vulnerable
version of Microsoft.AspNetCore.Components.WebAssembly.Server or
Microsoft.AspNetCore.Components.WebAssembly.DevServer to debug a Blazor
WebAssembly application and evaluate an expression involving
attacker-controlled application data." It adds: "Updating only the shared
runtime is not a substitute for updating these standalone packages."

**SecretPrinter.** It references neither package: its only packages are
`System.ServiceProcess.ServiceController` and `System.Diagnostics.EventLog`,
and its only framework is `Microsoft.NETCore.App`. No release folder holds a
file of either. It has no Blazor or WebAssembly code.

**Verdict.** Not reached.

## What this means for each release

| Release | Runtime | CVE-2026-111497 and CVE-2026-70197 | CVE-2026-114148 and CVE-2026-102870 |
| --- | --- | --- | --- |
| `0.5.0` | 10.0.14 | Corrected runtime | Not applicable |
| `0.3.0`, `0.4.0` | 10.0.13 | Affected runtime; not reached by SecretPrinter, as above | Not applicable |
| `0.1.0`, `0.2.0` | 10.0.12 | Affected runtime; not reached by SecretPrinter, as above | Not applicable |

`0.5.0` is the latest release. Moving to it settles the two runtime advisories
without relying on any of the reasoning here.

## What this does not show

- **That no other path reaches either runtime fault.** Microsoft's text is all
  that is known about what reaches them; this finding checks SecretPrinter
  against that text. No attack was tried, and the runtime was not tested.
- **That releases `0.2.0` and `0.3.0` are as their neighbours.** They were not
  downloaded. They are built from the same code paths for these questions and
  carry the same runtimes as `0.1.0` and `0.4.0`, by their changelog entries.
- **Anything about other software on the machines.** Other programs, SDKs and
  Visual Studio components on the development machine, FIOS-STB-01 or win11dev
  were not looked at.

## How it was measured

- Microsoft's pages in the browser pane, read-only, nothing clicked but the
  pages' addresses entered; the links in the announcement's table read with a
  short script in that page.
- The runtime's public source fetched from `raw.githubusercontent.com` at the
  tags `v10.0.12`, `v10.0.13` and `v10.0.14` of dotnet/runtime: `Socket.cs`,
  `Socket.Tasks.cs`, `SocketAsyncEventArgs.cs` and
  `SocketAsyncEventArgs.Windows.cs`.
- The release zips downloaded from this repository's Releases page with `curl`,
  each checked against its published `.sha256`.
- Three scratch tools in Claude's workspace, written for this and not kept in
  the repository, all reading assemblies with `System.Reflection.Metadata` and
  loading none: one lists type and member references, one lists fields and the
  fields chosen methods use, one hashes every method body.
- The restore records read through the link to the development machine.

## What changed

- This finding.
- `SECURITY.md`: the paragraph on the runtime each release carries now gives
  this outcome, with a note of what it said.
- Status notes on the findings of
  [the fifth release](2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md)
  and
  [the fourth release](2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md).
