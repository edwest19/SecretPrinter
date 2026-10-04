# Verification evidence

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

*The REQ-SEC-006 entry updated by Claude (Anthropic model, Claude Opus 5) at the
direction of Edwin West, 2026-09-21, when running as `LocalService` was measured.
Reviewed by a human before merge.*

*The REQ-SEC-010 entry updated by Claude (Anthropic model, Claude Opus 5) at the
direction of Edwin West, 2026-09-22, when a fifth disclosure was added to the
README. Reviewed by a human before merge.*

*The REQ-LIF-007 entry added by Claude (Anthropic model, Claude Opus 5.5) at the
direction of Edwin West, 2026-09-22, after the change was measured on
FIOS-STB-01. Reviewed by a human before merge.*

*The REQ-DIST-011 row under "Not yet evidenced" added by Claude (Anthropic
model, Claude Opus 5.5) at the direction of Edwin West, 2026-10-03. The
requirement was uncovered before then and was missing from that list. Reviewed
by a human before merge.*

*The REQ-DIST-011 entry added, and its row under "Not yet evidenced" removed, by
Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
2026-10-04, after a folder made by `publish-release.ps1` was run on a machine
with no .NET installed. Reviewed by a human before merge.*

*The REQ-DIST-004 and REQ-DIST-006 rows under "Not yet evidenced" reworded by
Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
2026-10-04, when the release workflow and `CHANGELOG.md` were written. They had
said that neither existed. Both requirements are still uncovered. The
REQ-DIST-004 row was corrected the same day, after the workflow's first run: it
had said the workflow had never run. Reviewed by a human before merge.*

*The REQ-DIST-004, REQ-DIST-005 and REQ-DIST-006 entries added, and their rows
under "Not yet evidenced" removed, by Claude (Anthropic model, Claude Opus 5.5)
at the direction of Edwin West, 2026-10-04, after the release workflow signed a
trial build and Edwin checked the signatures on a downloaded copy. Reviewed by a
human before merge.*

Some requirements in [README.md](../README.md) cannot be satisfied by code.
"Release binaries are signed" and "CI runs the specification checker" are
properties of the build and release process; no attribute on a class will ever
satisfy them.

This document records how those requirements are satisfied instead. It is read
by `tools/SecretPrinter.SpecCheck`, which treats an entry here as coverage —
**but only after confirming every artifact path named actually exists.**

## Rules

1. **Every entry names at least one artifact**, by repository-relative path. An
   entry with no artifact is an assertion, not evidence, and SpecCheck ignores
   it — leaving the requirement uncovered, which is the honest outcome.
2. **Paths are checked on every run.** If a file is renamed, moved or deleted,
   the entry is reported as `EVIDENCE BROKEN` and the check fails. Evidence
   cannot rot silently.
3. **Nothing is listed here before it exists.** A requirement whose artifact has
   not been built yet stays at `NOT IMPLEMENTED`. Writing a speculative entry
   would be the exact dishonesty this mechanism exists to prevent.
4. **Code coverage beats evidence.** Where a requirement has both an
   implementation and a test, SpecCheck reports it as code-covered and this
   entry becomes supporting context rather than the basis of the claim.

## What an entry proves

That the artifact exists and someone wrote down why it satisfies the
requirement. **Not** that the artifact does what the entry says it does.
SpecCheck confirms the reference is live; a reader confirms it is true.

Requirements covered this way are counted separately in every run, so the
proportion resting on evidence rather than tests is always visible.

---

## Evidence

| Requirement | Artifacts | How it is satisfied |
| --- | --- | --- |
| REQ-DIST-001 | `global.json` | Pins the SDK to the 10.0 band with `rollForward: latestFeature`, so every machine and CI build uses .NET 10 (LTS). Verified selecting 10.0.111 and 10.0.303 on different hosts. |
| REQ-DIST-002 | `Directory.Build.props` | Sets `TreatWarningsAsErrors`, `EnableNETAnalyzers` and `AnalysisLevel` for every project from one place. Individual project files no longer carry their own copies, so the setting cannot be quietly relaxed for one project. |
| REQ-DIST-007 | `src/SecretPrinter.Dns/DnsMessage.cs` | The single analyzer suppression in the repository — CA1720 on `DnsRecordType`, because `PTR` is the IANA name for DNS record type 12 — is applied at the declaration with a written justification. No global suppression file exists. |
| REQ-SEC-009 | `src/SecretPrinter.Dns/SecretPrinter.Dns.csproj`, `src/SecretPrinter.Spec/SecretPrinter.Spec.csproj`, `tools/SecretPrinter.Probe/SecretPrinter.Probe.csproj`, `tools/SecretPrinter.Listen/SecretPrinter.Listen.csproj`, `tools/SecretPrinter.SpecCheck/SecretPrinter.SpecCheck.csproj` | No project file contains a `PackageReference`. Every dependency is the .NET base class library or another project in this repository. Re-check by searching the repository for `PackageReference`. |
| REQ-SEC-010 | `README.md` | The statement that print job data passes through the proxy host appears in "Read this before installing", above the table of contents, as the first of five disclosures. |
| REQ-SEC-015 | `README.md` | The fourth disclosure in "Read this before installing", above the table of contents, states that the job is plain IPP between the client and the proxy and TLS between the proxy and the printer, and names the proxy as the point where it becomes encrypted. Section 3 repeats it against the architecture diagram, where step 4 is marked plaintext and step 5 TLS. |
| REQ-OBS-005 | `tools/SecretPrinter.Listen/README.md` | Carries a "Privacy note" section warning that captured mDNS traffic contains household device names, and instructing the reader to review output before publishing it. The tool prints the same warning in its `--help` text. |
| REQ-DIST-008 | `docs/verification.md` | This document. Recursive by construction: the requirement to record non-code evidence is itself satisfied by the file that records it, and SpecCheck confirms the file exists. |
| REQ-LIF-001 | `docs/findings/2026-09-04-windows-service-run.md`, `src/SecretPrinter.Service/WindowsService.cs` | The unit tests cover the start and stop semantics, but not the control-manager handshake, which no test can reach. That half rests on a recorded run: `sc.exe create`, `start` reaching `STATE : 4 RUNNING`, and `stop` accepted. Stop reached `STATE : 1 STOPPED` with exit code 0, so the shutdown path ran to completion within its timeout. Whether the goodbye records reached the network is noted in the finding as still unobserved. |
| REQ-SEC-006 | `docs/findings/2026-09-04-service-runs-unelevated.md`, `docs/findings/2026-09-18-running-as-localservice.md`, `docs/operating.md` | Measured, not assumed: the service bound UDP 5353 with `SO_REUSEADDR`, joined the multicast group on two interfaces, bound TCP 631 and relayed print jobs, all from an unelevated session with standard-user privileges. Since 2026-09-18 it has run as a Windows service under `NT AUTHORITY\LocalService` on FIOS-STB-01, and on 2026-09-21 it bound its sockets, joined both multicast groups, opened TLS to the printer and relayed printed pages under that account. The operating guide states both, and that `LocalService` has been measured on that one machine only. |
| REQ-DIST-003 | `.github/workflows/ci.yml` | Builds the solution in Release with warnings as errors, discovers every `tests/*.Tests` project and runs it with `--results`, then runs SpecCheck with all of those files and fails the job on a non-zero exit. Suite discovery is enumerated rather than listed so a missing `--test-results` flag cannot under-report coverage. |
| REQ-DIST-010 | `tests/SecretPrinter.TestKit/TestHarness.cs`, `tools/SecretPrinter.SpecCheck/CoverageMatrix.cs` | The harness records each exercised assembly's module version id; SpecCheck compares them against the assemblies it scans and fails on any mismatch. Verified by editing a source file, rebuilding without re-running the tests, and confirming the check named the rebuilt assembly and failed. |
| REQ-LIF-007 | `docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md`, `src/SecretPrinter.Service/RefusedStartService.cs`, `src/SecretPrinter.Service/Program.cs` | No test can reach the control manager, so this rests on a recorded run. On FIOS-STB-01, with `937de66` installed and the printer-side adapter disconnected, `Start-Service` failed and returned within three seconds; the service log recorded the refusal and its reason at 21:00:13Z; the System log recorded event 7023, "terminated with the following error: An exception occurred in the service when handling the control request", in the same second, with no 7009 timeout and no "timely fashion"; and `sc.exe query` reported `WIN32_EXIT_CODE : 1064`. Before the change, each of nine refusals was recorded as a timeout, event 7009, with event 7000 "did not respond ... in a timely fashion". Measured for one kind of refusal, the adapter being down, on one machine. |
| REQ-DIST-009 | `tools/SecretPrinter.SpecCheck/TestResultsDocument.cs`, `tests/SecretPrinter.TestKit/TestHarness.cs` | The harness records PASS/FAIL/SKIP per requirement; SpecCheck counts a requirement as tested only on a recorded PASS, and reports `TEST DID NOT RUN` otherwise. Verified by flipping a PASS to FAIL in a results file and confirming the requirement lost its coverage. |
| REQ-DIST-011 | `publish-release.ps1`, `docs/findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md`, `docs/operating.md` | The script publishes the service and the probe self-contained for `win-x64` into one folder, and fails unless each program's `runtimeconfig.json` lists an included runtime. A folder it made at `26ddfbe` was run on 2026-10-04 on a machine from which every .NET SDK and runtime had been removed: the service printed for an iPhone from a console window, and again installed as a service under `LocalService`, and the probe listed the printer's instances. `docs/operating.md` gives the release form of each step, and nothing a release needs uses the `dotnet` command. Limits, all in the finding: no release exists yet, so no release workflow has run the script; the probe is the only tool the documentation names, and the configuration tool must be added to the script when it exists; starting at boot and updating were not exercised. |
| REQ-DIST-004 | `.github/workflows/release.yml`, `docs/findings/2026-10-04-the-release-workflow-signed-a-build.md` | The sign job signs SecretPrinter's own `.exe` and `.dll` files with Azure Artifact Signing, the service the requirement calls by its earlier name, signing in by OpenID Connect with no stored secret. It fails unless every `.exe` and `.dll` in the folder then carries a valid signature, with SecretPrinter's under the one expected name and each with a timestamp, and unless no other file changed. Measured 2026-10-04 on the trial tag `v0.1.0-rc.2`: twelve files signed as `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, read by Edwin on a downloaded copy. Limits, in the finding: no release has been published, the job that publishes has never run, and the signed programs were not run. |
| REQ-DIST-005 | `docs/operating.md` | The section "Checking a release before you install it" gives two checks, the zip's SHA-256 against the published one and the signature on every program file, names the signer to expect, and states that a SecretPrinter file that is unsigned or signed under another name is not an official release. Both commands were run on the signed trial build on 2026-10-04. |
| REQ-DIST-006 | `.github/workflows/release.yml`, `CHANGELOG.md`, `Directory.Build.props` | The release workflow runs only for a tag, and its first job fails unless the tag matches `<Version>` in `Directory.Build.props` and `CHANGELOG.md` has exactly one dated heading for that version with text under it; that text becomes the release notes. The check passed for both trial tags on 2026-10-04. Its refusals (a tag that does not match, a missing, duplicated or empty entry) were exercised in Claude's container and not on GitHub. No release has been published yet; when one is, it can only be a tagged commit with a changelog entry. |

---

## Not yet evidenced

Nothing, as of 2026-10-04. SpecCheck reports no binding requirement as
uncovered.

This section listed REQ-DIST-004, REQ-DIST-005 and REQ-DIST-006 until that day,
and REQ-DIST-011 until earlier the same day. A requirement with no entry is
reported by SpecCheck as `NOT IMPLEMENTED` and fails the check, and belongs here
with what it is waiting on.
