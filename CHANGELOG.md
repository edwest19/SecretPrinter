# Changelog

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-04. Reviewed by a human before merge.*

Every release of SecretPrinter is a tagged commit with an entry here
(`REQ-DIST-006`). Versions follow [Semantic Versioning](https://semver.org/).

**How this file is used.** `.github/workflows/release.yml` refuses a tag unless
this file has exactly one heading of the form `## [version] - YYYY-MM-DD` for
it, and unless `<Version>` in `Directory.Build.props` says the same version.
Everything under that heading, up to the next one, becomes the release notes.

**No release has been published yet.** `0.1.0-rc.3` below is a pre-release for
testing, not a release. It was published on 2026-10-07. (Second sentence added
2026-10-07 by Claude, Claude Opus 5.5, and the third later that day, once it
was so.)

## [0.1.0-rc.3] - 2026-10-07

A pre-release for testing. **It is not release 0.1.0, and it carries no support
promise of any kind.** It exists so that two things are tried once before the
first release: the job that publishes a release, which has never run, and an
install on a clean machine from a zip downloaded with a browser, followed from
`docs/operating.md`.

- **Before you install it,** read two sections of `README.md` in the
  repository: "Read this before installing" and, under it, "Known problems".
  This software was written by an AI and has had no outside security review.
  Every measurement of it on a network was made in one household, with one
  printer model, an Epson ET-3760.
- **What it holds.** The service and the probe, for 64-bit Windows, with the
  .NET runtime they need beside them. It is configured by hand, by the steps in
  `docs/operating.md`; there is no configuration tool.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", has two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`.
- **The .NET runtime inside it** is whichever one the building machine's SDK
  supplied, and a fix to it arrives only in a new build. Its version is in
  `SecretPrinter.Service.runtimeconfig.json` in the folder. It is not known
  when this entry is written.
- **What changed since `0.1.0-rc.2`,** which was a trial and was not published:
  - A part of the running service that fails now stops the whole service,
    which says goodbye as it ends. Before, the rest ran on. Under the Windows
    service control manager the process then ends with exit code 4.
  - The mDNS responder survives a socket error while sending an answer, and
    logs it. Before, one such error ended its answering for good.
  - The example configuration prints `advertise.uuid` empty, as it prints the
    certificate fingerprint, and the service refuses to start until both are
    filled in. Before, it printed a fixed UUID that loaded.
  - The probe is held to its own security requirement, `REQ-SEC-016`, with
    tests that read its compiled files.
  - `README.md` was read against the code from its first line to its last and
    corrected, and gained the section "Known problems".
  - `docs/operating.md` was corrected from two walks through its steps on a
    second machine, and gained "What you will see on an iPhone".
- **What has not been shown,** at the time this entry is written:
  - The publishing job has never run. This tag is its first run.
  - What Windows shows when the service ends its own process after a failure.
    It is tested as far as it can be without Windows and has not been run
    under the service control manager.
  - `--print-example-config` from this build, and a start on its output, on
    Windows.
  - Anything on a printer other than the one model, or in another household.
- **What happened.** The workflow ran for this tag on 2026-10-07 and all three
  of its jobs passed, the publishing job on its first run. The signing job
  waited for Edwin West's approval. Twelve SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The pre-release is on the repository's Releases page as
  `SecretPrinter-0.1.0-rc.3-win-x64.zip`, with SHA-256
  `F8310DD6B5B061925958C1A25521C32E4C9979DC2BAE751E0C69ABCBC827C315`, and its `.sha256`
  file. When this is written nobody has downloaded and installed it; see
  [the finding](docs/findings/2026-10-07-the-release-workflow-published-a-pre-release.md).
  (Added 2026-10-07 by Claude, Claude Opus 5.5, after the run. It is not in
  the release notes GitHub shows, which were taken from this file at the tag.)
  (Added 2026-10-08 by Claude, Claude Opus 5.5: on 2026-10-08 it was
  downloaded with a browser on a second machine, checked by both checks,
  configured by hand, installed as a service by the steps of
  `docs/operating.md`, made to fail once on purpose, and printed through from
  an iPhone. It carries .NET 10.0.12. Two of the things listed above as not
  shown were shown that day: `--print-example-config` and a start on its
  output, and the service ending its own process after a failure, which
  Windows then showed as stopped with error 1067; see
  [the finding](docs/findings/2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md).)

## [0.1.0-rc.2] - 2026-10-04

The second trial of the release workflow. **This is not a release, and nothing
is published for it.**

- **Why it exists.** The first trial, `0.1.0-rc.1`, stopped before anything was
  signed. This one carries the correction and tries again.
- **What changed.** One line in `.github/workflows/release.yml`: the step that
  runs the specification check now ends with exit code 0 once it has recorded
  the check's result. Nothing in SecretPrinter itself changed.
- **Why nothing is published,** and what the build holds: as for `0.1.0-rc.1`
  below.
- **What happened.** The workflow ran for this tag and signed. Twelve
  SecretPrinter files came out signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, each with a timestamp,
  and the other 190 program files were still validly signed by Microsoft. The
  zip is 37,554,102 bytes with SHA-256
  `8A422569A5BBFB63C61385C5AE4117992CB3C3D173E8CB15B1F0D44A683C2A46`. It was
  kept with the workflow run for seven days and published nowhere. (Added
  2026-10-04 by Claude, Claude Opus 5.5.)

## [0.1.0-rc.1] - 2026-10-04

A trial of the release workflow. **This is not a release, and nothing is
published for it.**

- **Why it exists.** Signing can only be shown to work by signing something.
  This tag makes the release workflow run once from end to end: build, test,
  make the release folder, sign SecretPrinter's files, check every signature,
  and zip the result.
- **Why nothing is published.** The workflow publishes a GitHub Release only
  when the specification check passes. At this tag three binding requirements
  are uncovered: `REQ-DIST-004` (signing), `REQ-DIST-005` (how to check a
  signature) and `REQ-DIST-006` (this changelog and a tagged release). The
  signed zip stays with the workflow run, for examination.
- **What the build holds.** The service and the probe, published for 64-bit
  Windows with the .NET runtime beside them, as `publish-release.ps1` makes
  them. The configuration tool does not exist yet.
- **What happened.** The workflow ran once for this tag and failed in its first
  job. The step that runs the specification check recorded "not passed", as
  intended, and then ended with the check's own exit code of 1, which GitHub
  treats as a failed step. No release folder was made and nothing was signed.
  Claude had exercised that step without the wrapper GitHub puts around a
  PowerShell step, so the mistake did not show before the run. (Added
  2026-10-04 by Claude, Claude Opus 5.5.)
