# The probe was in the release and outside the security checks

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-06. Reviewed by a human before merge.*

**Status: corrected in the commit that carries this finding. Since 2026-10-04 a
release folder has held two programs, the service and the probe. The tests that
check what the code cannot do read the service's nine assemblies and did not
read the probe. Several places in the repository said those tests read every
"shipped" assembly, and one marker was false as written: it said no shipped
assembly outside `SecretPrinter.Mdns` and `SecretPrinter.Proxy` references a
socket type, and the probe opens a socket. The probe now has a requirement of
its own, `REQ-SEC-016`, a marker and six tests, and the wording is corrected.
The probe passed every new check as it stood. No statement it executes was
changed.**

## What was wrong

`SecretPrinter.Probe` began as a development tool. On 2026-10-04, in `26ddfbe`,
`publish-release.ps1` began publishing it into the release folder beside the
service, because configuring a release by hand depends on it (`REQ-DIST-011`).
The release workflow signs it with the service's files.

Nothing else was changed when that was done. These said "shipped", or
"installed", and went on describing only the service:

| Where | What it said |
| --- | --- |
| `src/SecretPrinter.Service/AssemblyClaims.cs`, the `REQ-SEC-003` marker | "No shipped assembly outside SecretPrinter.Mdns and SecretPrinter.Proxy references a socket type, so nothing else can open a port." |
| The same file, the `REQ-SEC-004`, `005`, `007` and `008` markers | "No shipped assembly references" `System.Diagnostics.Process`, a registry type, an HTTP client type; "No shipped assembly can run a command or write the registry". |
| The same file, its header | The tests read "the type-reference tables of every shipped assembly". |
| `tests/SecretPrinter.Service.Tests/SecurityClaimsTests.cs` | The list the tests read was named `ShippedAssemblies`, and its comment said diagnostic tools are excluded because "they are not installed". Three tests had names beginning "Nothing shipped". |
| `tests/SecretPrinter.Service.Tests/SecretPrinter.Service.Tests.csproj` | "by reading the compiled metadata of every shipped assembly". |
| `docs/operating.md`, "What you must do that the service will not" | "enforced by a test that fails if any shipped assembly so much as references a type capable of them". |
| `README.md`, the note on `REQ-SEC-005` under Dependencies | "a test reads the compiled metadata of every shipped assembly to confirm it". |

- **The first row was false.** The probe is shipped, it is outside those two
  projects, and it references `System.Net.Sockets.Socket`.
- **The second row was true of the probe and checked by nothing.** No test read
  the probe's assembly.
- **The rest described a test as reading something it did not read.**

README open question 9 disclosed part of this on 2026-10-05: that the probe is
not among the assemblies the checks read, and that the test file's comment had
stopped being true. It did not mention the markers, and it did not say that one
of them was false.

The mistake is Claude's. It put the probe in the release without searching the
repository for what was said about shipped and installed code.

## What the probe references

Read on 2026-10-06 in Claude's workspace, from assemblies built there from this
source, with `System.Reflection.Metadata`, the way the tests read them.

The probe's process loads three assemblies of this project: its own,
`SecretPrinter.Dns`, and, from this commit, `SecretPrinter.Spec`. They hold 103,
63 and 22 type references.

| Type | `SecretPrinter.Probe` | The other two |
| --- | --- | --- |
| `System.Diagnostics.Process`, `ProcessStartInfo` | no | no |
| `Microsoft.Win32.Registry`, `RegistryKey`, `RegistryHive` | no | no |
| `System.Net.Http.HttpClient`, `HttpRequestMessage`, `HttpClientHandler`, `System.Net.WebClient`, `WebRequest`, `HttpWebRequest` | no | no |
| `File`, `FileStream`, `FileInfo`, `StreamWriter`, `Directory`, `DirectoryInfo`, `IsolatedStorageFile`, `MemoryMappedFile`, in any namespace beginning `System.IO` | no | no |
| `System.Net.Sockets.Socket` | **yes** | no |
| `System.Net.Sockets.TcpListener`, `TcpClient`, `UdpClient`, `NetworkStream` | no | no |
| `System.Net.Security.SslStream` | no | no |

On the socket, the probe's assembly references these members and no others:
the constructor, `Bind`, `SetSocketOption`, `SendToAsync`, `ReceiveFromAsync` and
`LocalEndPoint`. It references neither `Listen` nor `Accept` nor `Connect`.

**One more reading, because it is the known weakness of this technique.** A type
reached by name at run time does not appear in the type-reference table. Of the
members of `System.Type`, the three assemblies reference only
`GetTypeFromHandle` and `op_Equality`, which the compiler emits for record
types. None references `Type.GetType`,
`System.Activator` or `System.Reflection.Assembly`, and the source has no
`DllImport`. This was read once, with a scratch program. No test checks it, for
the probe or for the service.

## What was decided

Whether to hold the probe to the service's checks was put to Edwin on
2026-10-06. He left it to Claude, the probe's design being Claude's. Claude's
decision:

- **Hold the probe to every check it can meet:** no other program started, no
  registry, no HTTP, no file written.
- **Not to the one it cannot meet.** It opens a UDP socket. That is its job, and
  the requirement says so instead of leaving it to be found.
- **Give it a requirement of its own.** The service's requirements each begin
  "The service", and the probe is not the service.

## What changed

- **`README.md`:** `REQ-SEC-016`, new. The Dependencies note and open question 9
  corrected.
- **`tools/SecretPrinter.Probe/AssemblyClaims.cs`:** new. One `[Requirement]`
  marker for `REQ-SEC-016` on the probe's assembly.
- **`tools/SecretPrinter.Probe/SecretPrinter.Probe.csproj`:** a reference to
  `SecretPrinter.Spec`, which defines that attribute and nothing else.
- **`tools/SecretPrinter.Probe/Program.cs`:** a note in its header saying which
  of its "does NOT" lines are now checked and which are not. No code.
- **`tests/SecretPrinter.Service.Tests/ProbeClaimsTests.cs`:** new, six tests,
  listed below.
- **`tests/SecretPrinter.Service.Tests/SecretPrinter.Service.Tests.csproj`:** a
  reference to the probe's project, so that its assembly is beside the tests to
  be read. **`Program.cs`** there registers the new class.
- **`tests/SecretPrinter.Service.Tests/SecurityClaimsTests.cs`:** the list
  renamed `ServiceAssemblies` and its comment rewritten; three test names and
  two messages say "the service" where they said "shipped". No test there
  changed what it checks.
- **`src/SecretPrinter.Service/AssemblyClaims.cs`:** the five markers and the
  header say "of the service" where they said "shipped". This changes text
  compiled into `SecretPrinter.Service.dll` and no behaviour.
- **`docs/operating.md`:** the sentence in the table above.

## The six tests, and each one broken on purpose

Each mutation added one temporary file to a project, the suite was built and
run, and the file was removed.

| Test | Mutation | Result |
| --- | --- | --- |
| The probe cannot start another program | `Process.Start` in the probe | failed, naming `SecretPrinter.Probe references System.Diagnostics.Process` |
| The probe cannot read or write the registry | `Registry.CurrentUser` in the probe | failed, naming `Microsoft.Win32.Registry` and `RegistryKey` |
| The probe cannot make an HTTP request | `new HttpClient()` in the probe | failed, naming `System.Net.Http.HttpClient` |
| The probe references no file-writing type | `File.Exists` in the probe | failed, naming `System.IO.File` |
| The probe opens a socket, and only its own assembly can | `new UdpClient()` in `SecretPrinter.Dns` | failed, naming `SecretPrinter.Dns references System.Net.Sockets.UdpClient`; the service's own socket test failed too, as it should |
| The probe is built on no project assembly these tests do not read | a reference from the probe to `SecretPrinter.Mdns`, and a use of one of its types | failed, naming `SecretPrinter.Probe references SecretPrinter.Mdns` |

In each run the named tests failed and every other test passed.

- **One assertion was not broken:** the first half of the fifth test, that the
  probe does reference `Socket`. Breaking it means taking the probe's socket
  away. It is there so that the absence checks cannot pass with nothing to
  look at.
- **A first attempt at the first mutation did not build.** It used
  `Process.GetCurrentProcess().Id`, which an analyzer rule turns into an error,
  and the script then ran the previous build and reported 98 passed. Claude saw
  the build error, changed the mutation and made the script stop when a build
  fails. Recorded because a mutation run that quietly tests the old build looks
  exactly like a test that does not catch anything.

The last test is there because of how this happened. The probe went unread
because a list that was right when written was not looked at when the probe was
shipped. That test fails if the probe gains a dependency on another assembly of
this project that the other five do not read.

## Where it was measured

**In Claude's workspace:** Linux, .NET SDK 10.0.112. NuGet is not reachable
there, so the service and its test project were compiled from the repository's
files in a scratch project, with the copy of
`System.ServiceProcess.ServiceController` that ships inside the SDK in place of
the package. That is not the build a release makes.

- All 98 tests of the service suite passed: the 92 that existed and the six new
  ones.
- The other six suites passed as before: 28, 25, 34 with 9 skipped, 61, 41 and
  121.
- SpecCheck, given that scratch build: 99 requirements, 23 assemblies, 18
  evidence rows, 422 test records, and `REQ-SEC-016` shown as implemented by
  `SecretPrinter.Probe` and tested. It still exits 1 there, with two
  requirements uncovered, because the tests that cover them are among the nine
  that skip on Linux. Before the marker and the tests were written, with only
  the new row in the README, it reported `REQ-SEC-016` as `NOT IMPLEMENTED`.
- The probe, built with the marker, printed its help text and exited 0.

**On the development machine,** which builds the real projects, Edwin ran every
suite and SpecCheck before committing. Those figures are in the commit message.

## What this does not show

- **The signed files of a release.** The tests read the probe as built for the
  test run. Publishing for `win-x64` compiles it again; whether that file is
  byte for byte the tested one is still not measured, for the probe or the
  service.
- **That the probe opens only one socket, or uses it only as its header says.**
  The tests show which capabilities are absent. `Listen`, `Accept` and `Connect`
  being unreferenced was read once and is checked by no test.
- **A type reached by name through reflection,** as above.
- **The probe run against a printer since the marker was added.** Its help text
  was printed in Claude's workspace. It was not run on a network.
- **The release folder with this change.** The folder already holds
  `SecretPrinter.Spec.dll`, for the service, so its file count should not
  change. That was not measured.
- **The other tools.** `Listen`, `Listen6`, `Loop6`, `Respond` and `SpecCheck`
  are in no release, and no test reads them.
