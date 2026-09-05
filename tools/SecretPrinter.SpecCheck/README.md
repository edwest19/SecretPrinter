# SecretPrinter.SpecCheck

Checks that `README.md` and the code agree with each other.

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

## What it does

1. Parses the requirement tables out of `README.md`.
2. Loads the compiled assemblies and reads their `[Requirement]` attribute
   metadata.
3. Prints a coverage matrix: for every requirement, which code implements it and
   which test verifies it.
4. Exits non-zero when a binding requirement is uncovered, when code cites a
   requirement the document does not define, or when an identifier is
   malformed.

The check runs in both directions. A requirement with no code is a gap; code
citing a requirement that no longer exists is equally a gap.

## What it proves — and what it does not

**A requirement marked `OK` has an implementation marker and a test marker. That
is all it means.**

An empty method carrying `[Requirement("REQ-PXY-004")]` counts as covered. This
tool finds *omissions*. It cannot judge whether the marked code does what the
requirement says, whether the test is meaningful, or whether a marker was placed
honestly.

A green run is not compliance. Reading the marked code is. Every run prints this
caveat at the end, so it cannot be quoted out of context.

## Running

From the repository root:

```
dotnet build
dotnet run --project tools/SecretPrinter.SpecCheck -- --readme README.md --search .
```

| Option | Meaning |
| --- | --- |
| `--readme <path>` | The specification document to parse. Required. |
| `--search <dir>` | Directory searched recursively for `SecretPrinter*.dll`. Repeatable. Every file used is printed. |
| `--assembly <dll>` | A specific assembly to scan. Repeatable. |
| `--test-results <path>` | Results file from a test run's `--results` option. Without it, a test that never ran still counts as coverage. CI must always supply this. |
| `--evidence <path>` | Document recording how non-code requirements are satisfied. Each entry must name artifact paths, and each path is checked to exist. |
| `--repo-root <dir>` | Root that evidence artifact paths resolve against. Defaults to the directory holding the README, and is echoed at startup. |
| `--write-matrix <path>` | Also write the matrix as Markdown. The only file this tool can write. |
| `--verbose` | Show every marker location instead of an abbreviation. |

At least one `--search` or `--assembly` is required; the tool does not guess
what to scan.

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Every binding requirement covered by code or intact evidence; no orphaned or malformed markers; no duplicate IDs; no evidence pointing at a missing artifact. |
| 1 | One or more of the above failed. |
| 2 | Bad arguments, or a file was not found. |
| 3 | The specification could not be parsed, or contained no requirements. |

Finding zero requirements is a failure rather than an empty pass. It almost
always means the document moved or its table format changed, and silently
reporting success would be the worst possible response.

## Test markers only count when the test ran

A `[Requirement]` attribute on a test method is compiled-in metadata. It is
present in the assembly whether or not the test ever executed, so a test that
skips on every machine would otherwise count as full coverage.

This was found in practice, not in theory. `REQ-CFG-006` needs a network adapter
holding two IPv4 addresses. Its test skipped on both the development container
and the target machine, and the matrix reported the requirement `OK` on both.

The test suites therefore write a results file:

```
PASS	REQ-ADV-013	SecretPrinter.Mdns.Tests.MdnsSocketTests.Open_sets_ttl_255
SKIP	REQ-CFG-006	SecretPrinter.Mdns.Tests.MdnsSocketTests.ResolveAll_rejects_shared_interface
```

Given `--test-results`, a requirement counts as tested only when a test carrying
its identifier PASSED. A skipped or failed test yields `TEST DID NOT RUN`, with
the reason, and fails the check.

Running without `--test-results` is permitted but never silent: the summary
reports `Test execution: NOT VERIFIED` and the result line says so rather than
claiming a clean pass.

## Results must describe the build being checked

A results file records the module version id of every SecretPrinter assembly the
run exercised. Builds are deterministic, so that id is a function of content and
changes exactly when the code does.

SpecCheck compares those fingerprints against the assemblies it scans, and
refuses any file produced against different code:

```
TEST RESULTS DO NOT DESCRIBE THIS BUILD:
  SecretPrinter.Advertising has been rebuilt since these results were produced
```

The mistake this catches is editing code, rebuilding, and re-running the check
without re-running the tests. It also catches a stale results file passed by a
mistyped filename, which is how the gap was found.

A results file containing no fingerprints at all is treated as stale, since
there is no way to tell what it describes.

## Evidence for non-code requirements

Some requirements cannot be satisfied by code. `REQ-DIST-004` ("release binaries
are signed") is a property of the release process; no attribute on a class will
ever satisfy it. Left unaddressed, such requirements sit permanently at
`NOT IMPLEMENTED`, the check can never pass, and a check that can never pass
gets ignored.

`--evidence docs/verification.md` supplies a document recording how those
requirements are satisfied instead:

```
| REQ-DIST-004 | .github/workflows/release.yml | The sign step invokes Azure Trusted Signing. |
```

Three properties keep this from becoming an escape hatch:

- **Artifact paths are checked to exist**, every run. Evidence pointing at a
  renamed or deleted file is reported as `EVIDENCE BROKEN` and fails. A claim
  that cannot rot unnoticed is worth something; free text is not.
- **Entries naming no artifact are ignored**, leaving the requirement uncovered.
  An assertion without an artifact is not evidence.
- **Evidence-covered requirements are counted separately** and shown as
  `OK (evidence)`, so the proportion resting on evidence rather than tests is
  always visible.

Evidence is permitted for any requirement. Deciding which requirements are
"really" about code is not a judgement this tool should make, so the safeguard
is visibility rather than restriction. Where a requirement has both code
coverage and evidence, code wins and the evidence becomes supporting context.

**What an entry proves:** the artifact exists, and someone wrote down why it
satisfies the requirement. Not that the artifact does what the entry claims.

## Conventions

**Test assemblies are identified by name.** An assembly whose simple name ends
in `.Tests` contributes *test* markers; every other assembly contributes
*implementation* markers. This is a convention the repository follows, not a
heuristic — a test project named otherwise would have its markers counted as
implementations.

**Identifiers match `^REQ-[A-Z]{3,4}-\d{3}$`.** The pattern is defined once, as
a constant on `RequirementAttribute`, so this tool and the attribute cannot
disagree about it.

## Two implementation choices worth knowing

**Attributes are read as metadata, not instantiated.** Scanned assemblies carry
their own copy of `SecretPrinter.Spec.dll`, so a `RequirementAttribute` from
that copy is a *different type* to the runtime than the one this tool compiled
against — a generic attribute lookup would silently return nothing and report
full coverage as zero. Matching on the attribute's full type name avoids that.
It also means no constructor from the scanned assembly ever runs.

**The README is parsed, not a sidecar file.** A YAML or JSON copy of the
requirements would be easier to parse and would begin drifting from the prose
humans actually read. Parsing the README guarantees the document a reader sees
and the document the tool checks are the same document. If the table format
changes, this parser breaks loudly, which is correct.

## Verification performed

Exercised against purpose-built assemblies covering every state the matrix can
report:

- A requirement with both an implementation and a test → `OK`
- Implementation only → `MISSING TEST`
- Test only → `MISSING IMPL`
- Neither → `NOT IMPLEMENTED`
- A marker citing an ID absent from the README → reported as orphaned
- A marker with a malformed ID → reported separately, without crashing
- The same attribute in a `.Tests` assembly → classified as a test
- A duplicated requirement row in the document → reported as a document defect
- Editing a source file, rebuilding, and re-running the check without re-running
  the tests → the rebuilt assembly is named and the check fails
- A skipped test → `TEST DID NOT RUN`, requirement loses coverage
- A `PASS` line flipped to `FAIL` → requirement loses coverage
- A results file containing only comments → every marked test unverified
- No `--test-results` supplied → `NOT VERIFIED`, stated in the result line
- A results path that does not exist → exit 2 with a message naming the fix
- A document with no requirements → exit 3, not a silent pass
- Missing file and missing argument paths → exit 2

Evidence handling was exercised separately:

- Seven real entries against existing artifacts → `OK (evidence)`, counted apart
  from code coverage
- An entry naming a file that does not exist → `EVIDENCE BROKEN`, check fails
- An entry citing an ID absent from the README → reported as orphaned evidence
- An entry naming no artifact at all → ignored, requirement left uncovered

The tool compiles with `TreatWarningsAsErrors` and zero warnings. One analyzer
finding (CA1859) was fixed rather than suppressed.

These checks were ad-hoc. Converting them into a committed test suite is a
tracked follow-up — and a slightly pointed one, since this tool's purpose is to
notice exactly that kind of gap.
