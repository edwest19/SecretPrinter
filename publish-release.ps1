# -----------------------------------------------------------------------------
# publish-release.ps1
#
# Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
# Edwin West, for the SecretPrinter project, 2026-10-03. Reviewed by a human
# before merge.
#
# Purpose:
#   Produces the folder a release is made from: the service and the probe,
#   published for 64-bit Windows with the .NET runtime beside them
#   ("self-contained"), which is the form REQ-DIST-011 requires of a release.
#
# Usage, from the repository root:
#   powershell -ExecutionPolicy Bypass -File .\publish-release.ps1 -OutputDirectory <folder>
#
# What it does not do:
#   It does not sign, package, tag or upload anything, and it does not run the
#   tests. It also proves nothing about a machine with no .NET installed: that
#   is a measurement, made by running the folder on such a machine, and this
#   script only produces the folder to measure.
#
# Why both programs go into one folder:
#   They need the same runtime, so one folder holds one copy of it. Each
#   program keeps its own .exe, .dll, .deps.json and .runtimeconfig.json.
#
# Why the output folder must be outside the repository:
#   SpecCheck searches the repository for SecretPrinter*.dll and keeps one file
#   per assembly name, chosen by path. A release folder inside the repository
#   puts a second copy of every assembly in its way, and it can pick that copy
#   instead of the one the tests ran against.
#
# Why the output folder must be new or empty:
#   Publishing over an existing folder adds and overwrites; it never removes.
#   A file left behind by an earlier publish would ship without anyone having
#   chosen it.
#
# What it leaves in the repository:
#   The build output for win-x64 under each published project's own bin and obj
#   folders, which git ignores.
#
# Why it anchors to $PSScriptRoot:
#   The project paths below are relative to the repository. The output folder
#   is the one path taken from the caller, so it is resolved against the
#   caller's location before the location changes.
# -----------------------------------------------------------------------------
[CmdletBinding()]
param(
    # Where the release folder is written. Must be outside the repository, and
    # must not exist yet or must be empty.
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'

# The one runtime releases are built for. README open question 10 records why.
$runtimeIdentifier = 'win-x64'

# What a release holds: the service, and every tool docs/operating.md tells
# someone using a release to run (REQ-DIST-011). Each entry names the project
# and the program that publishing it must produce.
$programs = @(
    @{ Project = 'src/SecretPrinter.Service'; Name = 'SecretPrinter.Service' },
    @{ Project = 'tools/SecretPrinter.Probe'; Name = 'SecretPrinter.Probe' }
)

# Resolved against the caller's location, and without requiring the folder to
# exist, so a relative path means what the caller meant by it.
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)

$separator        = [System.IO.Path]::DirectorySeparatorChar
$repository       = (Resolve-Path -LiteralPath $PSScriptRoot).ProviderPath
$repositoryPrefix = $repository.TrimEnd($separator) + $separator
$outputPrefix     = $output.TrimEnd($separator) + $separator

if ($outputPrefix.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The output folder '$output' is inside the repository '$repository'. " +
          "Choose a folder outside it: SpecCheck would find the published assemblies " +
          "and could check them instead of the ones the tests ran against."
}

if (Test-Path -LiteralPath $output) {
    if (-not (Test-Path -LiteralPath $output -PathType Container)) {
        throw "'$output' exists and is not a folder."
    }

    $existing = @(Get-ChildItem -LiteralPath $output -Force)
    if ($existing.Count -gt 0) {
        throw "The output folder '$output' is not empty ($($existing.Count) item(s)). " +
              "Publishing never removes files, so a release is published into a new or " +
              "empty folder. Nothing was changed."
    }
}

Push-Location $PSScriptRoot
try {
    foreach ($program in $programs) {
        Write-Host "--- publishing $($program.Project) ---"

        dotnet publish $program.Project `
            --configuration Release `
            --runtime $runtimeIdentifier `
            --self-contained true `
            --output $output

        if ($LASTEXITCODE -ne 0) {
            throw "Publishing $($program.Project) failed with exit code $LASTEXITCODE. " +
                  "The output folder may hold part of a release; delete it before trying again."
        }
    }
}
finally {
    Pop-Location
}

# What the publish must have produced. These are checks on the folder, not on
# the exit code: a publish can succeed and still not be the thing asked for.
Write-Host ""
Write-Host "Release folder : $output"
Write-Host "Built for      : $runtimeIdentifier"

foreach ($program in $programs) {
    $executable = Join-Path $output "$($program.Name).exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "'$executable' is missing. Publishing $($program.Project) did not produce its program."
    }

    # A self-contained program's runtimeconfig.json lists the runtime it carries
    # under 'includedFrameworks'. One that needs a runtime installed on the
    # machine lists what it needs under 'framework' or 'frameworks' instead, and
    # has no 'includedFrameworks' at all.
    $runtimeConfigPath = Join-Path $output "$($program.Name).runtimeconfig.json"
    if (-not (Test-Path -LiteralPath $runtimeConfigPath -PathType Leaf)) {
        throw "'$runtimeConfigPath' is missing, so what runtime $($program.Name) uses cannot be read."
    }

    $runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
    $included = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object { $null -ne $_ })
    if ($included.Count -eq 0) {
        throw "'$runtimeConfigPath' lists no included runtime. $($program.Name) was not " +
              "published self-contained and would need .NET installed on the machine (REQ-DIST-011)."
    }

    $carried = ($included | ForEach-Object { "$($_.name) $($_.version)" }) -join ', '
    Write-Host "Program        : $($program.Name).exe, carrying $carried"
}

$files = @(Get-ChildItem -LiteralPath $output -Recurse -File)
$bytes = ($files | Measure-Object -Property Length -Sum).Sum

Write-Host "Files          : $($files.Count)"
Write-Host "Bytes          : $bytes"
