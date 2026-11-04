# DRY RUN ONLY: attestation verification must reject every mismatched case. Never merge.
param([Parameter(Mandatory)][string]$Directory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = $env:GITHUB_REPOSITORY
$file = Get-ChildItem -LiteralPath $Directory -Filter 'blazorshop-api-*.tar.gz' | Select-Object -First 1
$tampered = Join-Path $env:RUNNER_TEMP 'tampered.tar.gz'
Copy-Item -LiteralPath $file.FullName -Destination $tampered
Add-Content -LiteralPath $tampered -Value 'x'
$unattested = Join-Path $env:RUNNER_TEMP 'unattested.tar.gz'
Set-Content -LiteralPath $unattested -Value 'never attested'

$strict = @('--repo', $repo, '--signer-workflow', "$repo/.github/workflows/release.yml", '--deny-self-hosted-runners')
$builtSource = @('--source-ref', $env:GITHUB_REF, '--source-digest', $env:GITHUB_SHA)
$cases = @(
    @{ Name = 'wrong source digest (approved 3ef5024 instead of the built commit)'; File = $file.FullName; Arguments = $strict + @('--source-ref', $env:GITHUB_REF, '--source-digest', '3ef50245922af23cc07ddd07ba03e64e0fc24ab4') }
    @{ Name = 'wrong source ref (a tag instead of the built ref)'; File = $file.FullName; Arguments = $strict + @('--source-ref', 'refs/tags/v0.0.0-rc.0', '--source-digest', $env:GITHUB_SHA) }
    @{ Name = 'wrong signer workflow (ci.yml)'; File = $file.FullName; Arguments = @('--repo', $repo, '--signer-workflow', "$repo/.github/workflows/ci.yml") + $builtSource }
    @{ Name = 'tampered archive'; File = $tampered; Arguments = $strict + $builtSource }
    @{ Name = 'unattested file'; File = $unattested; Arguments = $strict + $builtSource }
)

$accepted = 0
foreach ($case in $cases) {
    & gh attestation verify $case.File @($case.Arguments) *> $null
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) { $accepted++; Write-Host "[MISMATCH, accepted] $($case.Name)" }
    else { Write-Host "[ok, rejected] $($case.Name) => exit $exitCode" }
}
if ($accepted) { throw "$accepted attestation case(s) were accepted" }
