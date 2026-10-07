# The README was read against the code

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-07. Reviewed by a human before merge.*

**Status: done for the README, with limits. On 2026-10-07 Claude read
`README.md` from its first line to its last against the code at `e42c034` and
against the findings, as the step before a first release. Nine statements were
false as written, six had gone stale, and fourteen things the code does, or
does not do, were stated nowhere in the README. All are corrected or stated in
the commit that carries this finding, each with a dated note where it stands.
The command the README gave a reviewer was run as printed on a fresh clone and
reported that nothing was implemented. One new section, "Known problems", now
sits near the top of the README. No statement that runs was changed: the only
changes to compiled text are one marker's wording and the authorship metadata.
What was not read is listed at the end.**

No IPv6 address, MAC address, device name or printer host name appears in this
finding.

## What was read

- **`README.md`,** every line.
- **The findings of 2026-10-06,** both, and each earlier finding a correction
  below rests on.
- **The service's code,** in full: `SecretPrinter.Responder` (both files),
  `SecretPrinter.Advertising` (three), `SecretPrinter.Proxy` (four),
  `SecretPrinter.Configuration` (two), `MdnsSocket.cs`, `MdnsInterface.cs` and
  `InterfaceInventory.cs`, `PrinterResolver.cs` and `PrinterReachability.cs`,
  and all fifteen files of `SecretPrinter.Service`: `ServiceHost`, `Program`,
  `Offering`, `StartupWait`, `PrinterWatch`, `PrinterSide`, `ListenPlan`,
  `AvailabilityGate`, `AdvertisementLog`, `FileServiceLog`, `ServiceLog`,
  `WindowsService`, `ServiceLifecycle`, `RefusedStartService` and
  `AssemblyClaims`.
- **`docs/operating.md`, `docs/verification.md`, `SECURITY.md`, `CHANGELOG.md`,
  `Directory.Build.props`, `global.json`, `ci.yml`** and the two scripts that
  run the tests and the check, in full.
- **In part:** `SecretPrinter.Dns` (its structure, and the guards in its
  reader), the probe's `Program.cs` (its header and its use of its socket),
  `release.yml` (its jobs, permissions and conditions), SpecCheck (searched for
  what Section 11 says of it), and two tests.

## How each thing was checked

By reading, in most cases. Three things were measured, all in Claude's
workspace, on Linux with .NET SDK 10.0.112:

- **The reviewer's command,** on a fresh clone. See the next section.
- **What the responder sends,** in twelve cases, with a scratch program that
  drives the real `MdnsResponder` over the test kit's fake transport. The cases
  and the results are in
  [the finding on RFC 6762](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md).
- **The version stamp,** read from a built assembly: it holds
  `0.1.0-rc.2+e42c0340388e6dee0130b4773867f67ce3629ad3`, the version and the
  commit the clone was on.

Five public repositories were also read for their existence, for the sentence
under "About the name"; see "Left open".

## The reviewer's command reported that nothing was implemented

Section 11, under "For an AI agent reviewing this repository", gave one step to
run:

```
dotnet run --project tools/SecretPrinter.SpecCheck -- \
  --readme README.md --evidence docs/verification.md --search .
```

and said: "It reports which requirements are implemented, tested, evidenced, or
none of these."

Run exactly so, in a fresh clone of `e42c034`, it built SpecCheck and what
SpecCheck refers to, two assemblies in all, and nothing else. SpecCheck reads
built assemblies, so it reported:

| Line of its summary | Value |
| --- | --- |
| Requirements | 99 |
| Assemblies | 2 |
| covered by code | 0 |
| covered by evidence | 18 |
| binding gaps | 79 |
| Test execution | NOT VERIFIED (no --test-results supplied) |
| Result | FAIL |

and 81 rows read `NOT IMPLEMENTED`. Every one of those is a true report of a
tree in which nothing has been built. Together they tell a reviewer who followed
the README's one instruction that the project implements none of its
specification. The step has stood since the first commit, `699598b`, of
2026-09-05, and nobody is on record as having followed it from a clean tree.

It now says to run `run-tests.ps1` and then `run-speccheck.ps1`, says why the
build has to come first, and gives these figures. The mistake is Claude's: the
step was written by a model that always had a built tree in front of it.

## Statements that were false as written

| Where | What it said | What is so | How it is known |
| --- | --- | --- | --- |
| README, `REQ-ADV-012` | The service responds "only to queries for service types it advertises". | It answers for five kinds of name: its two service types, the DNS-SD enumeration name `_services._dns-sd._udp.local`, its instance name and its host name. | Read in `MdnsResponder.HandleAsync` and `AdvertisementBuilder.Build`; measured, in the finding on RFC 6762. |
| README, `REQ-SEC-001`, and the marker for it in `MdnsResponder.cs` | The service answers "with its own advertisement, and with `NSEC` records ... and nothing else"; the marker: "no byte of a received packet is ever re-emitted". | An answer sent by unicast to a legacy querier repeats the query's identifier and its question, as RFC 6762 §6.7 requires. | Read; measured: identifier `0xBEEF` and the question came back. |
| README, Section 3 | "During development the target printer moved twice." | The record holds two addresses in one session, which is one move. | [The finding of 2026-09-01](2026-09-01-printer-capabilities.md). |
| README, Section 11, steps 2 and 3 | Two code examples: a marker on `internal sealed class IppRelay`, and a test `Relay_does_not_alter_payload_bytes` carrying `[Fact]`. | Neither is in the repository. The real marker is on a method of a public class; the real test is `Payload_reaches_the_printer_unchanged` and carries `[TestCase]`, from this repository's own test kit. `[Fact]` belongs to a test framework the project does not use. | Read in `IppRelay.cs` and `IppRelayTests.cs`; a search for `[Fact]` finds it only in a comment. |
| README, Section 11, the reviewer's step | As above. | As above. | Measured. |
| `docs/verification.md`, `REQ-SEC-009` | "No project file contains a `PackageReference`." | One does: `SecretPrinter.Service.csproj`, for the package the README documents under Dependencies. | A search of the 23 project files. |
| `docs/operating.md`, Configuring | The service "does not start if either does not answer". | Since 2026-09-22 it starts, offers nothing and keeps asking (`REQ-LIF-008`). | Read in `StartupWait.cs`; the README has said so since that day. |
| `Directory.Build.props`, and through it every assembly | `AuthoredBy`: "Claude (Anthropic model, Claude Opus 4.5)". | Three models wrote the code, as the README's first paragraph says. | The file headers. |
| `run-speccheck.ps1` | Its message for a missing results file: "Re-run test1.ps1 first." | The script is `run-tests.ps1`. No file named `test1.ps1` is in this repository's history. | `git log`. |

The authorship metadata is compiled into every program a release would hold,
signed ones included, which is why it is corrected before a release and not
after.

## Statements that had gone stale

| Where | What it said | Since when it was not so |
| --- | --- | --- |
| README, fifth item under "Read this before installing", and Section 3 | Windows was measured choosing the default route on three occasions (Section 3: two). | 2026-10-06, when a fourth was measured and recorded in that day's finding, and not carried to the README. Section 3 had been one behind since 2026-10-03. |
| README, open question 1 | "TLS is not deferred; it is the remaining blocker to printing." | 2026-09-17. |
| README, open question 5; `docs/operating.md`, Privileges; `docs/verification.md`, `REQ-SEC-006` | `LocalService` has run on one machine. | 2026-10-04, when it was registered, started and printed on a second. The account of the running process was not read there, and all three places now say that too. |
| README, Section 11, step 3c | The module version id "changes exactly when the code does". | 2026-10-04, when the version and the commit began to be stamped into every assembly. |
| `SECURITY.md`, "What is in scope" | "The CI workflow and, once it exists, the release workflow." | 2026-10-04. |
| `docs/operating.md`, exit code 4 | "An interface, socket or the printer was unavailable." | 2026-09-22: a printer that does not answer is waited for. |

## Things the code does that the README did not say

Each is now stated, in a note on the row it belongs to or in the text named.

1. **It publishes the printer's maker and model.** `REQ-ADV-009` names six TXT
   keys. The code copies fourteen, three of which (`usb_MFG`, `usb_MDL`,
   `product`) name the printer, and adds three of its own. Now in the row and
   in the third item at the top.
2. **It publishes a pointer under `_services._dns-sd._udp.local`.** Now in
   `REQ-ADV-012`.
3. **The goodbye and the announcements go out over IPv4 only.** It was in
   `docs/operating.md` and in three findings. Now in `REQ-LIF-003`.
4. **It asks the printer, and connects to it, over IPv4 only.** Now in
   Section 3.
5. **The permitted IPv4 client network is worked out from the address.** The
   first three numbers of the client interface's address, or the first alone
   when it begins with 10; the adapter's subnet mask is not read. Now in
   `REQ-SEC-012`.
6. **The certificate's dates are not read,** by a decision of 2026-09-15
   recorded in `CertificatePin.cs`. Now in `REQ-SEC-013`.
7. **A handshake below TLS 1.2 is abandoned.** Now in `REQ-PXY-012`.
8. **A connection cut by the service's own stop or withdrawal has no ending
   line in the log.** Four findings record such connections, and the latest
   asks whether `REQ-OBS-006` covers them. Read in `IppRelay.RelayOneAsync`: a cancellation
   by the service is not caught there and reaches no observer call, by design
   ("reports failures and deliberately does not report cancellations",
   `Connections.cs`). So the answer is that no requirement covers it, and
   `REQ-PXY-009` now says so.
9. **A recovery can fail to offer the printer.** If the probe or the
   announcement cannot be sent when the printer answers again, nothing is
   offered until the printer is lost and found again or the service is
   restarted. The code logs this and its comments call it a known consequence.
   Now in `REQ-LIF-006`.
10. **A client interface needs IPv6, and that is found late.** Now in
    `REQ-CFG-005` and `docs/operating.md`. Read, not run.
11. **A failure after start-up is not reported to Windows.** See the next
    section.
12. **`ServiceBase` may write to the Application event log at each start and
    stop,** not only when a start fails. Now beside the dependency it comes
    with. Not measured.
13. **A release carries its own .NET, which only a new release replaces.** Now
    under Dependencies, with Microsoft's words for it.
14. **Four things RFC 6762 asks of a responder are not done.** They have
    [a finding of their own](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md).

Two statements were not false and claimed more than is known, and are now
worded to what is known:

- **`REQ-ADV-013`** read as met without qualification. Whether an IPv4 answer
  sent by unicast carries TTL 255 has been open since
  [2026-09-06](2026-09-06-unicast-ttl-gap.md). The row now says so.
- **The release paragraph of Section 12** said the signing job "waits for a
  person's approval". The requirement is a setting of a GitHub environment,
  outside this repository, and whether the run of 2026-10-04 waited is not
  recorded.

One requirement was reworded, on purpose: **`REQ-OBS-005`**, a `SHOULD`. It
read "Logs note that observed mDNS traffic may contain device names". No log
line says so. The note is in the listening tool's README, which is what
`docs/verification.md` has always named as the evidence, and in
`docs/operating.md`. The requirement now says where the note is, and the
evidence row names both files. This is the one change to the coverage matrix.

## A service that fails after it has started

Noticed on 2026-09-22 and written into
[that day's finding](2026-09-22-a-refused-start-is-reported-as-a-timeout.md) as
"reasoned and not observed", and then not carried anywhere: not into the README,
not into `docs/operating.md`, and not into the lists of open work.

Read again today, it is as described. Under the Windows service control
manager, `OnStart` returns as soon as the work is under way. If the work then
fails, `ServiceLifecycle` records the exception and logs `Service stopped
because of an error`. Nothing calls `ServiceBase.Stop`, and `ServiceBase.Run`
returns only when the service stops. So the process stays, and Windows would
go on showing the service as running.

Two requirements describe failures of this kind as stopping the service:
`REQ-LIF-004` ("a fast, loud failure") and `REQ-ADV-021` ("logs why, says
goodbye and stops, to be restarted"). The first now carries a note. Both are
true of what the service does and silent on what Windows is told.

It has not been run. It is listed under "Known problems" as not yet corrected.

*(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5: two
things in this section did not stand. "Both are true of what the service does"
was wrong of `REQ-ADV-021`: the code logged that it was stopping and did not
stop the service. Claude had read the log line and the `throw` and had not
followed the exception to the `Task.WhenAll` that waited on it. And the fault
is now corrected in the code, though still not run under Windows. The Known
problems entry, the notes on `REQ-LIF-004` and `REQ-ADV-021`, and the
paragraph in `docs/operating.md` are rewritten to say so. See
[the finding](2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md).)*

## The section "Known problems"

The README had no one place that said what is known to go wrong. The facts were
in requirement rows, in open questions and in findings. A person deciding
whether to install would have had to read all of them. The new section, under
"Read this before installing", lists eleven, each with the finding or the
requirement behind it. A sixth numbered item above it says how narrowly the
software has been measured: one household, one printer model, an iPhone and one
other device, three machines.

Nothing in the section was unrecorded before today except two of the four from
RFC 6762. The rest was on record and not gathered.

## What Claude decided, and why

On 2026-10-06 Edwin twice declined to rule on engineering that Claude had
proposed, and said it was Claude's to decide. These were decided on that
basis, and each is his to overturn.

- **State, do not build.** Every item above that could be built instead of
  stated was stated: the unicast TTL, the ending line for a cut connection, the
  client network read from the mask, the IPv6 check at load. Each would change
  code a release has been measured with, and this piece of work was the
  reading.
- **The port 80 links and the printer's `Server` header are stated,** in the
  third item at the top. A finding of 2026-09-29 had left that to Edwin. It is
  a statement of what was measured, and he can have it reworded or removed.
- **Known problems sit at the top,** not in a section of their own further
  down, so that no section number or link changes.
- **The examples in Section 11 are now real code,** quoted with the file each
  comes from, so that a reader can check them.

## Mistakes Claude made during this work

- **Drafted an install step that would have broken the install.** The first
  correction for the stray copy of the configuration moved the file out of the
  release folder with `Move-Item`. A file moved within one disk can keep the
  permissions of the folder it came from, and a folder under a user's profile
  does not let `LocalService` read; `docs/operating.md` records exactly that
  failure for a build under a profile. The step now leaves the two measured
  `Copy-Item` commands as they were and adds one line that removes the stray
  copy. The permission reasoning was not measured.
- **Wrote that nine tests skip "off Windows".** Claude's working note from the
  previous session said the same. They skipped in Claude's workspace because it has no
  IPv6: the skip message is `Could not bind UDP 5353 in this environment:
  AddressFamilyNotSupported`. Corrected before delivery to what was measured.
- **Wrote that all three print attempts that sent no job showed a message.**
  The finding records the message for two. Corrected before delivery.

## Seen and not corrected

Comments in the code that no longer describe it. None changes what the code
does, and each is a line or two; they are left for one pass of their own, so
that this commit changes no source file but the one marker.

- `MdnsSocket.cs`, in `OpenIPv6`: "Neither does anything yet: nothing receives
  on this socket and nothing sends on it. REQ-ADV-019 and REQ-ADV-020 remain
  unmet." Both are met and marked.
- `MdnsSocket.cs`, on `ReadBackIPv6SendState`: "None of these three read-backs
  has ever been executed on this project." Tests execute them on Windows.
- `WindowsService.cs`: "NOT COMPILED BY THE AUTHOR", and that the file "was
  never built or run before being handed over". It has been built and run
  since.
- `RefusedStartService.cs` and `Program.cs`: what the control manager is told
  is "not yet observed under the control manager", and "has not yet been run
  under the control manager". It was measured on 2026-09-22.
- `AvailabilityGate.cs`, on its constructor: "so it starts open". The service
  starts it closed.
- `Program.cs`, the `--help` text for exit code 4: "an interface, socket or the
  printer was unavailable".
- The probe's `Program.cs` header lists two rounds of questions; the code sends
  a third, for addresses.
- `RequirementAttribute.cs` has `[Fact]` in an example in a comment.
- `tests/SecretPrinter.TestKit/TestHarness.cs` writes "Written by
  tests/SecretPrinter.Mdns.Tests" into every suite's results file.
- The machine name of the development machine is in three findings, in
  `MdnsSocket.cs` and in `MdnsSocketTests.cs`. Whether it stays is Edwin's.

*(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5: two
of these are now corrected, in the change that
[the finding of the same day](2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md)
records: the `WindowsService.cs` paragraph has a note beside it, and the
`--help` text for exit code 4 is rewritten. The rest stand.)*

*(Status note, 2026-10-07, later again, by Claude, Claude Opus 5.5: the rest
are corrected, in the commit that carries this note. Each file's header says
what was changed. All are comments but one: the line the test harness writes
at the top of a results file, which nothing reads. On the last item, Edwin
West decided the same day that the development machine's Windows name does not
stay. It is replaced by "the development machine" in the three findings, each
with a note, and in the two source files. It remains in this repository's
history, which was not rewritten: that would change every commit hash the
findings cite. The check for household strings that is run before every
delivery did not look for a machine name of that form, and now does.)*

## Left open

- **"About the name".** The README says of a family of projects that "every one
  of them is fully open, with its source, its reasoning, and its limitations
  published". The five it names were read on 2026-10-07 and are public
  repositories. Whether every project with the prefix is, and whether each
  publishes its reasoning and limitations, Claude cannot tell. Put to Edwin.
- **Whether the four from RFC 6762 are built before a first release.** Edwin's.
  *(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5:
  decided by Edwin West. They are stated in release 0.1.0 and built after it.
  The reasons are in
  [that finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md).)*
- **The service that fails after start.** To be corrected, and then run once
  on a machine.
  *(Status note, 2026-10-07, later the same day, by Claude, Claude Opus 5.5:
  corrected in the code. The run on a machine is still owed.)*
- **The `Remove-Item` line added to the install step** has not been run.
- **`SECURITY.md`, "Supported versions",** still says no release has been
  tagged and that the section will be replaced at the first release. That is
  for the release itself.

## What this does not show

- **That the tests check what their markers say.** The tests were not read,
  but for two.
- **The files not read:** `PrinterInterfaceReport.cs`, `MdnsBinding.cs`,
  `IMdnsTransport.cs`, `RequirementAttribute.cs`, the probe's `Report.cs`, the
  other tools, `publish-release.ps1` beyond what the README says of it, and
  most of `SecretPrinter.Dns` and SpecCheck.
- **Anything new on a Windows machine.** Nothing was run on one for this
  finding but the suites and the check, which Edwin ran on the development
  machine before committing. Those figures are in the commit message.
- **The findings themselves.** They were read as the record. Only the four
  given a status note today were changed.
- **That nothing else is wrong.** One reading found what is above. The
  reviewer's step had stood since the first commit.

## Where it was checked

In Claude's workspace, with all eleven changed files and both new findings in
place: the six suites that run there passed as before (28, 25, 34 with 9
skipped, 61, 41 and 121), and the service suite, compiled there from the
repository's files against the copy of its one package inside the SDK, passed
all 98. SpecCheck, given that build: 99 requirements, 23 assemblies, 18
evidence rows, 422 test records, 2 binding gaps, exit code 1, the same as
before the changes; the two gaps are the requirements whose tests skip there.
The coverage matrix written before and after differs in one row, `REQ-OBS-005`,
whose evidence now names two files.
