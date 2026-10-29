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

  *(Status note, 2026-10-29, by Claude, Claude Opus 5.5: the query suite has
  now been read, from a run's own log rather than from the settings page: it
  is the default suite. The log of the "Analyze (csharp)" job of run 21, for
  `9f58175` at 19:33Z on 2026-10-29, carries the workflow GitHub generates for
  default setup, packed into its `CODE_SCANNING_WORKFLOW_FILE` value.
  Unpacked, its step "Initialize CodeQL" passes `queries: ""`, with the
  comment `# Default query suite`, to `github/codeql-action/init@v4`, for both
  jobs. The same log shows CodeQL 2.27.3 loading 59 queries for C# from the
  pack `codeql/csharp-queries` 1.10.2. The generated workflow names no
  schedule (it runs `on: dynamic`), so how often the schedule runs is still
  not read. GitHub's page "Configuring default setup for code scanning" speaks
  of "the weekly schedule", and from 2026-10-22 to 2026-10-28 one scheduled
  run was seen. The log was opened by its address, the one the job page links
  to for the raw log, and the workflow was unpacked by a short script in the
  page; nothing was clicked or changed.)*
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

*(Status note, 2026-10-29, by Claude, Claude Opus 5.5: the results of two of
them were read on 2026-10-29, read-only, in the browser pane. **Dependabot
alerts:** "There are no open Dependabot alerts for this repository", 0 open
and 0 closed, with the dependency files last checked at 18:41:44Z on
2026-10-28. **The dependency graph** lists 10 dependencies: nine are the
GitHub Actions of the two workflow files, each pinned to a commit, and one is
the NuGet package `System.ServiceProcess.ServiceController` 10.0.11, from
`src/SecretPrinter.Service/SecretPrinter.Service.csproj`. It does not list
`System.Diagnostics.EventLog` 10.0.11, which that package brings with it and
which ships; the README's Dependencies table lists both. So "no open alerts"
was reported over a graph that does not list that package. Whether Dependabot
would raise an alert for it is not shown: GitHub's page "Dependency graph
supported package ecosystems", as read with the fetch tool, does not say
whether transitive NuGet dependencies are detected. **Secret scanning:** "No
secrets found", 0 open and 0 closed. No alert on these pages means only that
nothing matched; like CodeQL, neither is a review. The pages were opened by
their addresses, and nothing on them was clicked or changed.)*

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
