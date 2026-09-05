# -----------------------------------------------------------------------------
# run-speccheck.ps1
#
# Written by Claude (Anthropic model, Claude Opus 5) at the direction of
# Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
#
# Purpose:
#   Runs the specification checker against the results files that run-tests.ps1
#   produced, and prints its exit code.
#
# Run run-tests.ps1 first. SpecCheck compares assembly module version ids and
# refuses results produced against different code, so stale files fail rather
# than quietly counting.
#
# A non-zero exit is expected while requirements remain uncovered. It reports
# what is missing; it does not judge whether marked code is correct.
# -----------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $suites  = Get-ChildItem -Path tests -Directory -Filter '*.Tests'
    $results = Get-ChildItem -Path artifacts -Filter '*.Tests.txt' | Sort-Object Name

    if ($results.Count -ne $suites.Count) {
        throw "Expected $($suites.Count) results file(s), found $($results.Count). Re-run test1.ps1 first."
    }

    $resultArgs = $results | ForEach-Object { '--test-results', $_.FullName }

    dotnet run --project tools/SecretPrinter.SpecCheck --configuration Release --no-build -- `
        --readme README.md `
        --evidence docs/verification.md `
        @resultArgs `
        --write-matrix artifacts/coverage-matrix.md `
        --search .

    Write-Host "`nSpecCheck exit code: $LASTEXITCODE"
}
finally {
    Pop-Location
}