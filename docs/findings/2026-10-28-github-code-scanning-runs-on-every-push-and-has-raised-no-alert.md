# GitHub's code scanning runs on every push and has raised no alert

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-28. Reviewed by a human before merge.*

**Status: read on GitHub on 2026-10-28, read-only. CodeQL, GitHub's code
scanning tool, has analysed this repository since 02:45Z on 2026-10-22, in
GitHub's "default setup", which is switched on in the repository's settings on
GitHub and has no file in this repository. To 18:43Z on 2026-10-28 it had run
19 times, every run passed, and it had raised no alert. Until this finding
nothing in the repository said it runs. It is an automated check of code
patterns, not a security review.**

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name.

## Why this was looked at

Runs named CodeQL have appeared on the repository's Actions page since
2026-10-22, beside CI and the release workflow. The handoffs carried them as
"not looked into". Edwin West asked for them to be looked at, and then for what
was found to be recorded here and in `SECURITY.md`.

## What was seen

- **Where it is set up.** The repository's settings on GitHub, under Advanced
  Security, list "CodeQL analysis" as "Default setup". No file under `.github`
  configures it; the repository has two workflow files, `ci.yml` and
  `release.yml`.
- **When it was switched on.** The first run, named "CodeQL Setup", started at
  02:45:08Z on 2026-10-22 (10:45 pm Eastern on 2026-10-21), "Triggered via
  dynamic" by Edwin West's account, on commit `43dde3b`. That is how GitHub
  records default setup being switched on.
- **When it runs.** On each push to `main` (runs 2 to 5 and 7 to 19), and on a
  schedule: run 6, at 12:22:16Z on 2026-10-24, by
  `github-advanced-security[bot]`. How often the schedule runs was not read.
- **What it reads.** Each run has two jobs, "Analyze (actions)" and "Analyze
  (csharp)", on GitHub's `ubuntu-latest` machines. Its status page says "C#
  109/109 files scanned" and "GitHub Actions 2/2 files scanned". The repository
  tracks 109 C# files (41 under `src`, 54 under `tests`, 14 under `tools`) and
  2 workflow files, so it reads the tests and tools as well as what a release
  ships.
- **What it has reported.** The code scanning page: "0 Open", "0 Closed",
  "There aren't any code scanning alerts yet". Last scan 18:43:01Z on
  2026-10-28, the run for `fec29f7`. All 19 runs show "completed successfully".
  Each job of the first and the latest run carries one notice, that "The
  ubuntu-latest label will migrate to Ubuntu 26 beginning October 19, 2026";
  that concerns GitHub's machines, not SecretPrinter, whose CI and release
  builds run on `windows-latest`.
- **Its other settings, as the page shows them.** "Copilot Autofix" is on: it
  suggests fixes for CodeQL alerts. Check runs fail at "Security alert severity
  level: High or higher" and "Standard alert severity level: Only errors".

## What this does not show

- **Which queries it runs.** The query suite, and the schedule's frequency, are
  behind a menu on the settings page that was not opened.
- **That the code is free of faults.** No alert means CodeQL's queries matched
  nothing. It is not a review, and `SECURITY.md` still says, truly, that the
  project has had no outside security review.
- **Who can see the alerts page.** It was read in the browser pane of the
  Claude desktop app on the development machine, which was signed in to GitHub.

## Also on the settings page, not looked into further

Private vulnerability reporting is on, as `SECURITY.md` says. The page also
lists the dependency graph "On", Dependabot alerts "On", Dependabot security
updates "Off", malware alerts "Off", secret protection with push protection
"On", and the AI scan features "Off". Their results were not read.

## How it was read

In the browser pane of the Claude desktop app on the development machine: the
Actions page filtered to CodeQL, the pages of runs 1 and 19, the code scanning
alerts page, CodeQL's tool status page, and the settings page for Advanced
Security. Pages were opened by their addresses and nothing on them was clicked
or changed. The run times were read from the pages' date elements by a short
script in the page. The file counts were made in a clone of `fec29f7`.

## What changed

- This finding.
- `SECURITY.md`: a paragraph under "Read this first", after the one on outside
  review, saying that CodeQL runs and what it has found.
