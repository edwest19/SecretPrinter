# Verification evidence

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

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
| REQ-SEC-010 | `README.md` | The statement that print job data passes through the proxy host appears in "Read this before installing", above the table of contents, as the first of three disclosures. |
| REQ-OBS-005 | `tools/SecretPrinter.Listen/README.md` | Carries a "Privacy note" section warning that captured mDNS traffic contains household device names, and instructing the reader to review output before publishing it. The tool prints the same warning in its `--help` text. |
| REQ-DIST-008 | `docs/verification.md` | This document. Recursive by construction: the requirement to record non-code evidence is itself satisfied by the file that records it, and SpecCheck confirms the file exists. |
| REQ-LIF-001 | `docs/findings/2026-09-04-windows-service-run.md`, `src/SecretPrinter.Service/WindowsService.cs` | The unit tests cover the start and stop semantics, but not the control-manager handshake, which no test can reach. That half rests on a recorded run: `sc.exe create`, `start` reaching `STATE : 4 RUNNING`, and `stop` accepted. Stop reached `STATE : 1 STOPPED` with exit code 0, so the shutdown path ran to completion within its timeout. Whether the goodbye records reached the network is noted in the finding as still unobserved. |
| REQ-SEC-006 | `docs/findings/2026-09-04-service-runs-unelevated.md`, `docs/operating.md` | Measured, not assumed: the service bound UDP 5353 with `SO_REUSEADDR`, joined the multicast group on two interfaces, bound TCP 631 and relayed print jobs, all from an unelevated session with standard-user privileges. The operating guide states this and marks what remains unmeasured (running under `LocalService`). |
| REQ-DIST-003 | `.github/workflows/ci.yml` | Builds the solution in Release with warnings as errors, discovers every `tests/*.Tests` project and runs it with `--results`, then runs SpecCheck with all of those files and fails the job on a non-zero exit. Suite discovery is enumerated rather than listed so a missing `--test-results` flag cannot under-report coverage. |
| REQ-DIST-010 | `tests/SecretPrinter.TestKit/TestHarness.cs`, `tools/SecretPrinter.SpecCheck/CoverageMatrix.cs` | The harness records each exercised assembly's module version id; SpecCheck compares them against the assemblies it scans and fails on any mismatch. Verified by editing a source file, rebuilding without re-running the tests, and confirming the check named the rebuilt assembly and failed. |
| REQ-DIST-009 | `tools/SecretPrinter.SpecCheck/TestResultsDocument.cs`, `tests/SecretPrinter.TestKit/TestHarness.cs` | The harness records PASS/FAIL/SKIP per requirement; SpecCheck counts a requirement as tested only on a recorded PASS, and reports `TEST DID NOT RUN` otherwise. Verified by flipping a PASS to FAIL in a results file and confirming the requirement lost its coverage. |

---

## Not yet evidenced

Listed for visibility. These have no entry above, so SpecCheck reports them as
`NOT IMPLEMENTED` and the check fails — correctly.

| Requirement | What it is waiting on |
| --- | --- |
| REQ-DIST-004 | No release workflow, and no signing step, has been written. |
| REQ-DIST-005 | Signature verification instructions cannot be written before signing exists. |
| REQ-DIST-006 | No release has been tagged; `CHANGELOG.md` does not exist. |
