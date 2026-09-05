# -----------------------------------------------------------------------------
# run-tests.ps1
#
# Written by Claude (Anthropic model, Claude Opus 5) at the direction of
# Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
#
# Purpose:
#   Builds in Release and runs every test suite, writing one results file per
#   suite into artifacts/.
#
# Why not 'dotnet test':
#   The suites are console applications, not VSTest projects. 'dotnet test'
#   finds no test projects, builds the solution, prints 'Build succeeded' and
#   runs nothing - which reads like a pass. This mirrors what ci.yml does.
#
# Why it anchors to $PSScriptRoot:
#   Every path below is relative to the repository. Launched with -File, a
#   PowerShell profile can change the working directory first, and the paths
#   then resolve somewhere else entirely.
# -----------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'

# Paths below are relative to the repository, so anchor to this script's own
# folder rather than to wherever the caller happened to be standing.
Push-Location $PSScriptRoot
try {
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }

    $suites = Get-ChildItem -Path tests -Directory -Filter '*.Tests' | Sort-Object Name
    if ($suites.Count -eq 0) { throw "No test suites found under tests/." }

    New-Item -ItemType Directory -Force -Path artifacts | Out-Null

    foreach ($suite in $suites) {
        $results = Join-Path 'artifacts' "$($suite.Name).txt"
        Write-Host "--- $($suite.Name) ---"
        dotnet run --project $suite.FullName --configuration Release --no-build -- --results $results
        if ($LASTEXITCODE -ne 0) { throw "$($suite.Name) failed." }
    }

    Write-Host "`nAll $($suites.Count) suite(s) passed."
}
finally {
    Pop-Location
}