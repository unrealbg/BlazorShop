# DRY RUN ONLY: the publishing job must reject swapped artifacts. Never merge.
param([Parameter(Mandatory)][string]$Directory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$guard = Join-Path $PSScriptRoot '..' 'release-guard.ps1'
$copy = Join-Path $env:RUNNER_TEMP 'swapped'
Copy-Item -LiteralPath $Directory -Destination $copy -Recurse
$archive = Get-ChildItem -LiteralPath $copy -Filter 'blazorshop-storefront-*.tar.gz' | Select-Object -First 1
Add-Content -LiteralPath $archive.FullName -Value 'x'

$cases = @(
    @{ Name = 'swapped storefront archive'; Directory = $copy; Sums = $env:SUMS_SHA256 }
    @{ Name = 'manifest hash from another build'; Directory = $Directory; Sums = ('0' * 64) }
)
$accepted = 0
foreach ($case in $cases) {
    & pwsh -NoProfile -File $guard -Command VerifyArtifacts -Directory $case.Directory -ExpectedSumsSha256 $case.Sums -ReleaseFiles $env:RELEASE_FILES *> $null
    if ($LASTEXITCODE -eq 0) { $accepted++; Write-Host "[MISMATCH, accepted] $($case.Name)" }
    else { Write-Host "[ok, rejected] $($case.Name) => exit $LASTEXITCODE" }
}
if ($accepted) { throw "$accepted hand-off case(s) were accepted" }
