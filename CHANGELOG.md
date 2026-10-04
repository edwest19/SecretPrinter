# Changelog

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-04. Reviewed by a human before merge.*

Every release of SecretPrinter is a tagged commit with an entry here
(`REQ-DIST-006`). Versions follow [Semantic Versioning](https://semver.org/).

**How this file is used.** `.github/workflows/release.yml` refuses a tag unless
this file has exactly one heading of the form `## [version] - YYYY-MM-DD` for
it, and unless `<Version>` in `Directory.Build.props` says the same version.
Everything under that heading, up to the next one, becomes the release notes.

**No release has been published yet.**

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
