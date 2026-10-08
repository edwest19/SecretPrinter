# The release workflow signed a build

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-04. Reviewed by a human before merge.*

**Status: `REQ-DIST-004` is measured. On 2026-10-04 the release workflow ran for
the trial tag `v0.1.0-rc.2` at `3bcdd4c`, signed SecretPrinter's twelve program
files with Azure Artifact Signing, and kept the signed zip with the run. Edwin
downloaded it and read every signature himself. The first trial, `v0.1.0-rc.1`,
had failed before anything was signed, because of a mistake of Claude's in the
workflow. Neither trial is a release, and no release exists.**

The name on the signing certificate appears below in full. It is in every file
the certificate signs, and Edwin agreed to its being written here.

## What was set up beforehand (2026-10-03, by Edwin)

- **In Azure:** an app registration for this repository alone, with no
  certificate and no secret. It holds one federated credential, whose subject is
  `repo:edwest19@5809459/SecretPrinter@1357850472:environment:release`: this
  repository, by name and by GitHub's numeric ids, in the GitHub environment
  `release`. The registration has the role *Artifact Signing Certificate Profile
  Signer* on the signing account `edwest19`.
- **On GitHub:** the environment `release`, set to accept only tags beginning
  with `v` and to require a reviewer. It holds three values the workflow needs
  to sign in. They are identifiers, not passwords.
- **Nothing that can sign is stored anywhere.** GitHub gives the sign job a
  short-lived token saying which repository and environment it runs in, and
  Azure accepts that token only for the subject above.

## The first run failed, and why

| | |
| --- | --- |
| Tag | `v0.1.0-rc.1`, at `650f60c` |
| Run | Release #1, `actions/runs/37229980190` |
| Result | failed after 1 minute 15 seconds |
| Reported | one error, `Process completed with exit code 1.`, and one warning, the workflow's own message that the specification check had not passed |
| Kept from the run | `build-record` only. No release folder was made and nothing was signed |

- **The cause.** The step that runs the specification check is meant to record
  "not passed" and let the run go on, so that a build can be signed for
  examination while requirements are still uncovered. It recorded that. But
  GitHub ends a PowerShell step with the exit code of the last program the step
  ran, and that program was SpecCheck, which had exited with 1.
- **Why it was not caught.** Claude exercised the workflow's steps in its
  container by running each step's script directly. GitHub wraps a PowerShell
  step in a line of its own that exits with that code. Run with the same
  wrapper, the step as pushed exits with 1, and the corrected step exits with 0.
- **The correction** (`3bcdd4c`) is one line: the step ends with `exit 0` once
  the result is recorded.
- **The tag was left where it is.** A second trial, `0.1.0-rc.2`, was made
  for the corrected workflow. A tag that is moved to another commit stops
  meaning anything.

## The second run signed

| | |
| --- | --- |
| Tag | `v0.1.0-rc.2`, at `3bcdd4c` |
| Run | Release #2, `actions/runs/37231905990` |
| Result | Success, 13 minutes 44 seconds from start to finish |
| Reported | one warning, the same one: the specification check had not passed, so no GitHub Release would be published |
| Kept from the run | `build-record` (26.4 KB), `unsigned-folder` (35.6 MB), `signed-release` (35.7 MB) |

`signed-release` is uploaded by the last step of the sign job. For it to exist,
every step before it passed:

- the folder arrived with SecretPrinter's `.exe` and `.dll` files unsigned and
  every other `.exe` and `.dll` validly signed;
- the sign-in to Azure was accepted;
- the signing action ran;
- afterwards every `.exe` and `.dll` carried a valid signature, SecretPrinter's
  all under one name and each with a timestamp;
- by SHA-256, no file had changed since the build job made the folder except
  SecretPrinter's own `.exe` and `.dll` files.

Three things the workflow file said only a real run could show are therefore
shown: Azure accepted the sign-in, a job holding only `id-token: write` could
fetch the build job's files, and the certificate's name is now known.

The repository's Releases page read "There aren't any releases here" after the
run, as intended.

## What Edwin measured on the downloaded copy

He downloaded `signed-release` from the run's page and unpacked it on the
development machine.

| File | Bytes |
| --- | --- |
| `SecretPrinter-0.1.0-rc.2-win-x64.zip` | 37,554,102 |
| `SecretPrinter-0.1.0-rc.2-win-x64.zip.sha256` | 104 |
| `release-notes.md` | 563 |

The SHA-256 recorded in the `.sha256` file and the SHA-256 he measured from the
zip were the same:
`8A422569A5BBFB63C61385C5AE4117992CB3C3D173E8CB15B1F0D44A683C2A46`.

`Get-AuthenticodeSignature` on every `.exe` and `.dll` in the unpacked folder:

| Files | Status | Signer | Timestamp |
| --- | --- | --- | --- |
| 12, SecretPrinter's own | `Valid` | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` | yes |
| 185 | `Valid` | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | yes |
| 3 | `Valid` | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | yes |
| 2 | `Valid` | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | yes |

That is 202 program files, none unsigned. The twelve are the nine
SecretPrinter assemblies, `SecretPrinter.Probe.dll`, and the two programs,
by count; they were not listed by name in this check.

## What Microsoft's documentation says about these certificates

Read 2026-10-04
([certificate management](https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-cert-management)):

- The signing certificates are renewed daily and are valid for 72 hours.
- So the certificate's thumbprint and public key change from one day to the
  next, and checking against either does not last. The name stays the same.
- A timestamp is what keeps a signature valid after its certificate has
  expired.

This is why the workflow, and the instructions in
[`operating.md`](../operating.md), compare the signer's **name**, and why the
workflow fails a SecretPrinter file that is signed without a timestamp.

## What this does not show

- **No release has been published.** The job that publishes has never run: it
  is skipped unless the specification check passes, and it had not.
- **The signed programs were not run** before this finding was written. The
  folder the workflow signs is made by the same script as the folder that
  printed on 2026-10-04
  ([finding](2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)),
  but that folder was built on the development machine and was unsigned.
  *(Status note, 2026-10-06, by Claude, Claude Opus 5.5: they were run on
  2026-10-06. The signed zip of this run was checked, unpacked and configured
  by hand on a second machine, the service was run from a console window, and
  an iPhone printed through it
  ([finding](2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).
  That finding also records the dates on the certificate that signed these
  files, and that its signature still read `Valid` after it had expired.)*
- **Whether the sign job waited for approval.** The environment was set to
  require a reviewer. Claude cannot read, without signing in to GitHub, whether
  the run stopped for one, and Edwin's account of it is not recorded here.
  *(Status note, 2026-10-07, by Claude, Claude Opus 5.5: recorded after all.
  The run's public page, `actions/runs/37231905990`, shows the `release`
  environment with "edwest19 approved Oct 4, 2026" and the comment "verified".
  Why that was not found on the page when this finding was written is not
  established. See
  [the finding of 2026-10-07](2026-10-07-the-release-workflow-published-a-pre-release.md).)*
- **The run's own summary was not read.** It holds the workflow's table of the
  signer and the zip's SHA-256. What is above comes from the public run page
  and from the downloaded files.
- **Whether the shipped files are the tested files** is still open, as the
  other finding of this date says.
- **The runner's .NET SDK, and so the runtime this build carries,** were not
  read.

## Left on GitHub

- Two tags, `v0.1.0-rc.1` and `v0.1.0-rc.2`. Neither is a release.
- The second run's `signed-release` and `unsigned-folder`, kept for 7 days,
  and its `build-record`, kept for 30. Anyone signed in to GitHub can download
  them from the run's page until then.
