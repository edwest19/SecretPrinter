# The first release was published

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-08. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-08 Edwin West pushed the tag `v0.1.0` on
the commit that sets the version to 0.1.0, and the release workflow ran for the
fourth time. All three of its jobs passed, the signing job after his approval.
The job that publishes published "SecretPrinter 0.1.0" on the repository's
Releases page, not marked as a pre-release: the first time it has published a
version without a hyphen. A copy downloaded with a browser on a second machine
passed both checks in [`operating.md`](../operating.md). It carries .NET 10.0.12
and is stamped with the tagged commit. Nothing in this build has been run.**

*(Status note, 2026-10-09, by Claude, Claude Opus 5.5: this build was run on
2026-10-09. It was downloaded on FIOS-STB-01, passed both checks there, and was
installed as the service in place of a build from source; it found the printer
and an iPhone printed through it. See "What this does not show" below.)*

No IPv6 address, MAC address or device name appears in this finding. The second
machine is called the test machine, as in the findings it follows.

## The commit and the tag

- **The commit** is `27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d`. It sets
  `<Version>` to `0.1.0`, adds the changelog entry that the workflow publishes
  as the release notes, and rewords the places that said no release existed.
  No source file changed after `0.1.0-rc.3`, so its programs are compiled from
  the same source code
  ([the finding on that pre-release](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md)).
  Edwin ran every suite and SpecCheck on it before committing; the figures are
  in its commit message. Claude cloned the push and compared all 197 tracked
  files byte for byte, and CI run 124 is listed as successful.
- **The go-ahead.** Asked whether now was the time to publish, with that as the
  recommendation, Edwin answered "publish now".
- **The tag** `v0.1.0` is annotated, names the commit by its full hash, and git
  records it at 2026-10-08 19:09:52 -0400, which is 23:09:52 UTC.

## The run

Release #4, `actions/runs/37857871852`. What follows is from Edwin's two
screenshots of the run's page, the summary he pasted, and the public pages
Claude read afterwards through a tool that fetches a page and answers questions
about it.

- **Status:** Success, total duration 7 minutes 0 seconds: the build job 1
  minute 29 seconds, the signing job 2 minutes 27 seconds, the publishing job
  10 seconds. Three files kept with the run.
- **The build job's coverage matrix** is headed "82 of 99 requirements fully
  covered", as for `0.1.0-rc.3`. The other seventeen rest on evidence.
- **The approval.** The first screenshot shows the dialog "Review pending
  deployments", with `release` ticked, "Review needed from edwest19", and the
  comment "reviewed". Edwin approved it. The second shows the banner "The
  deployments have been approved."
- **The signing job's summary,** as Edwin pasted it:

  | | |
  | --- | --- |
  | Tag | `v0.1.0` |
  | Commit | `27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d` |
  | Files in the folder | 216 |
  | Files signed in this run | 12 |
  | Signed by | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
  | Zip | `SecretPrinter-0.1.0-win-x64.zip` |
  | SHA-256 of the zip | `7006676752EE2C767D264EAB810E835F1D56A4FF88F19DE89D277994DA209D22` |
  | Specification check | pass |

## What was published

Read on the public pages a few minutes after the run:

- **The release:** "SecretPrinter 0.1.0", tag `v0.1.0`, commit `27b5f5c`,
  marked Latest and not marked Pre-release, published by the workflow's own
  account at 23:16 UTC. Its notes begin "The first version of SecretPrinter
  meant as a release.", the first line of the changelog entry.
- **Two files,** both dated 23:16:52 UTC: `SecretPrinter-0.1.0-win-x64.zip`,
  shown as 35.8 MB, and `SecretPrinter-0.1.0-win-x64.zip.sha256`, shown as 99
  bytes. GitHub adds its two archives of the source.
- **The digest** GitHub shows for the zip,
  `7006676752ee2c767d264eab810e835f1d56a4ff88f19de89d277994da209d22`, is the
  value in the signing job's summary, letter for letter apart from case.

99 bytes is what the `.sha256` file should be: 64 characters of hash, two
spaces, the 31 characters of the zip's name, and a line ending of two bytes.

**The first release without `--prerelease`.** The publishing job adds that
option only when the version has a hyphen. Until this run it had published one
version, `0.1.0-rc.3`, with it. This is the first time it ran without it, and
GitHub marked the result Latest.

## The downloaded copy

On the test machine, in Microsoft Edge, by Edwin, at about 23:30 UTC: "it did
not complain. no warnings." Then, in Windows PowerShell:

- **Check 1,** as `operating.md` prints it: `Get-FileHash` gave
  `7006676752EE2C767D264EAB810E835F1D56A4FF88F19DE89D277994DA209D22`, and the
  `.sha256` file holds the same, two spaces, and the zip's name.
- **Unpacked** with the `Expand-Archive` command `operating.md` has given since
  2026-10-08, into a new folder: 216 files.
- **Check 2,** with a column for the timestamp:

  | Count | Ours | Status | Signer | Timestamped |
  | --- | --- | --- | --- | --- |
  | 185 | False | `Valid` | `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | True |
  | 2 | False | `Valid` | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | True |
  | 3 | False | `Valid` | `CN=.NET DAC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` | True |
  | 12 | True | `Valid` | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` | True |

- **The runtime** in `SecretPrinter.Service.runtimeconfig.json`:
  `Microsoft.NETCore.App 10.0.12`, the same as `0.1.0-rc.3` and `0.1.0-rc.2`.
  The changelog entry could not say this when it was written.
- **The stamp** in `SecretPrinter.Service.dll`:
  `0.1.0+27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d`, the version and the commit
  the tag names.

Claude had asked for this to be done on the development machine. Edwin did it
on the test machine, which serves the same purpose here: nothing was installed
or run.

Every prediction Claude gave before each command held.

## What this does not show

- **This build, run.** Its programs were checked and not run. Its source code
  is that of `0.1.0-rc.3`, which was installed from nothing and printed through
  on the same day; the two builds differ in the version and the commit stamped
  into each file. *(Status note, 2026-10-09, by Claude, Claude Opus 5.5: run
  that day on FIOS-STB-01, which runs Windows 10, installed by the update steps
  of `operating.md`; an iPhone printed through it
  ([finding](2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md)).)*
- **That the signing job cannot run without approval.** An approval is recorded
  for this run, as for the two before it. The setting that requires it belongs
  to the GitHub environment and has not been read.
- **What any job logged.** The logs need a sign-in and were not read.
- **The zip's size in bytes.** GitHub shows 35.8 MB; the downloaded file's
  length was not printed.
- **Anything after publishing:** who downloads it, and on what.

## Changed in the commit that carries this finding

Each change is dated where it stands.

- `CHANGELOG.md`: the paragraph at the top says `0.1.0` is the first release
  and was published on 2026-10-08; the `0.1.0` entry gains "What happened". The
  release notes GitHub shows were taken from this file at the tag and do not
  change.
- `README.md`, section 12; `SECURITY.md`, "Supported versions";
  `docs/operating.md`, "Where the program comes from" and "Checking a release
  before you install it": that 0.1.0 was published on 2026-10-08, and the
  results above.
- `docs/verification.md`: this finding is named as evidence for `REQ-DIST-004`,
  `REQ-DIST-005` and `REQ-DIST-006`, so the coverage matrix changes in those
  three rows.
