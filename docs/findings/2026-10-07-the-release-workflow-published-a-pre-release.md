# The release workflow published a pre-release

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-07. Reviewed by a human before merge.*

**Status: measured, once. On 2026-10-07 Edwin West pushed the tag
`v0.1.0-rc.3` and the release workflow ran for the third time. All three of its
jobs passed. The job that publishes ran for the first time and published a
pre-release on the repository's Releases page. The signing job waited for
Edwin's approval, and the run's page records the approval. The same page for
the second trial run of 2026-10-04 records one too, which had been left as
"not recorded" since that day. The zip's SHA-256 as the signing job recorded it
is the digest GitHub shows for the published file. Nobody has yet downloaded
the zip, read its signatures or run what is in it.**

## Why a pre-release

Two things had never been tried, and the first release would otherwise have
been the first try of both: the job that publishes, and an install on a clean
machine from a zip downloaded with a browser. Claude put three courses to
Edwin on 2026-10-07: a pre-release first; straight to `0.1.0`; or an unsigned
folder built on the development machine, which publishes nothing and tries
neither. Claude recommended the first and Edwin chose it.

## Before the tag

- **The commit.** `c5cc8c4` sets `<Version>` to `0.1.0-rc.3`, adds the
  changelog entry that becomes the release notes, and rewords the places that
  said no build of SecretPrinter existed. Edwin ran every suite and SpecCheck
  on it before committing; the figures are in its commit message. Claude
  cloned the push and compared all 195 tracked files byte for byte.
- **Private vulnerability reporting.** `SECURITY.md` has told people to report
  through it since it was written, and whether it was switched on could not be
  told from outside. Edwin switched it on that evening, before the tag. His
  screenshot of the repository's Advanced Security settings shows the row
  "Private vulnerability reporting" offering "Disable", under the banner
  "Repository settings saved."

## The run

Edwin tagged the commit by its full hash and pushed the tag. The tag is
annotated and names `c5cc8c4ba5f58b438a42b3a8aefc382f9487056b`; git records it
at 2026-10-07 18:47:04 -0400, which is 22:47:04 UTC.

What follows is from three places: the run's summary, which Edwin pasted; his
own account; and the run's public page,
`actions/runs/37698359502`, which Claude read signed in as nobody. That page
shows the jobs, the approval and the kept files. It did not show the jobs'
summaries, and the logs behind it need a sign-in and were not read. Claude
reads a web page through a tool that fetches it and answers questions about
it; what is quoted from the page below was not compared with the page by eye.

- **The run:** Release #3, triggered by the push, status Success, total
  duration 4 minutes 49 seconds.
- **The three jobs,** by the page: build, 1 minute 31 seconds; sign, 1 minute
  43 seconds; publish, 21 seconds.
- **Kept with the run:** `build-record`, 27.9 KB; `signed-release`, 35.7 MB;
  `unsigned-folder`, 35.6 MB.
- **The build job** published the coverage matrix in its summary, headed "82
  of 99 requirements fully covered". The other seventeen rest on evidence and
  are marked so. The specification check: pass.
- **The signing job waited.** Edwin: "yes i approved it waited". The page: the
  `release` environment was approved by `edwest19` on Oct 7, 2026, with the
  comment "reviewed". Its summary:

  | | |
  | --- | --- |
  | Tag | `v0.1.0-rc.3` |
  | Commit | `c5cc8c4ba5f58b438a42b3a8aefc382f9487056b` |
  | Files in the folder | 216 |
  | Files signed in this run | 12 |
  | Signed by | `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` |
  | Zip | `SecretPrinter-0.1.0-rc.3-win-x64.zip` |
  | SHA-256 of the zip | `F8310DD6B5B061925958C1A25521C32E4C9979DC2BAE751E0C69ABCBC827C315` |
  | Specification check | pass |

- **The publishing job** ran, for the first time in this project. The summary
  Edwin pasted has no section for it; that it passed is from the run's status
  and from what was published.

## The approval of 2026-10-04, read at last

[The finding of 2026-10-04](2026-10-04-the-release-workflow-signed-a-build.md)
lists "whether the sign job waited for approval" among the things it does not
show, saying Claude could not read it without signing in, and the README has
said since that it "is not recorded". That finding drew on the run's public
page, so the page was read that day; why the approval was not found on it is
not established. The page for the second trial run,
`actions/runs/37231905990`, read on 2026-10-07, shows the `release`
environment with the line "edwest19 approved Oct 4, 2026" and the comment
"verified". It shows the publishing job at 0 seconds, which is the job being
skipped, and one warning, that the specification check did not pass and no
release would be published.

What a recorded approval shows is that one was given. That the job would not
have run without it is the environment's setting, which is still not read.

## What was published

Read by Claude on the public pages, signed in as nobody, a few minutes after
the run.

- **The release:** "SecretPrinter 0.1.0-rc.3", tag `v0.1.0-rc.3`, commit
  `c5cc8c4`, marked Pre-release and not Latest, published by the workflow's
  own account. The notes are the changelog entry.
- **Two files:** `SecretPrinter-0.1.0-rc.3-win-x64.zip`, shown as 35.8 MB, and
  `SecretPrinter-0.1.0-rc.3-win-x64.zip.sha256`, shown as 104 bytes, both
  dated 22:51:54 UTC. GitHub adds its two archives of the source.
- **The digest.** GitHub shows a SHA-256 for the zip of
  `f8310dd6b5b061925958c1a25521c32e4c9979dc2bae751e0c69abcbc827c315`. It is the
  value in the signing job's summary, letter for letter apart from case.

104 bytes is what the `.sha256` file should be if it holds what the workflow
writes: 64 characters of hash, two spaces, the 36 characters of the zip's
name, and a line ending of two bytes. Its contents were not read.

## What this does not show

- **The published files, downloaded.** Nobody has downloaded the zip. Its
  SHA-256 has not been computed on a downloaded copy, the `.sha256` file has
  not been read, and no signature in it has been read outside the workflow.
  The tool Claude reads web pages with is not permitted to fetch a release's
  files, and Claude did not go round that.
- **The programs.** Nothing in this build has been run.
- **The .NET runtime it carries.** Not read.
- **That the signing job cannot run without approval.** An approval is
  recorded for this run and for the second trial run. The setting that makes
  the job wait belongs to the GitHub environment, outside this repository,
  and was not read.
- **What any job logged.** The logs need a sign-in and were not read.

## Next

An install from nothing on a second machine, from this zip downloaded with a
browser, following `docs/operating.md` step by step. It will make the checks
this finding could not, and run two things that have never run on Windows:
the example configuration as the program now prints it, and a service that
ends its process when it fails after starting.
