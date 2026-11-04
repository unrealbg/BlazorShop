# DRY RUN ONLY: R11 source guards against real commits of this repository. Never merge.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseGuard.psm1') -Force

$repo = $env:GITHUB_REPOSITORY
$approved = '3ef50245922af23cc07ddd07ba03e64e0fc24ab4'
$failedE2e = '238a336baa26d1350423c2e4c1b288fabf108865'
$prHead = '964c6bb7fc5ce8650786081aafb21bba8a90b73d'
$experiment = '8371ceff33e4171c6a72a45eb2403c15150994f6'

$cases = @(
    @{ Name = 'approved master commit 3ef5024: master history and 4 required checks'; Expect = 'pass'; Test = {
        Assert-OnMaster -Repository $repo -Sha $approved
        $evidence = @(Get-RequiredCheckEvidence -Repository $repo -Sha $approved)
        if ($evidence.Count -ne 4) { throw "evidence count $($evidence.Count)" }
        $evidence | Format-Table name, check_run_id, workflow_run_id, workflow, conclusion | Out-String | Write-Host } }
    @{ Name = 'master commit 238a336 with failed browser-e2e'; Expect = 'required-checks'; Test = {
        Assert-OnMaster -Repository $repo -Sha $failedE2e
        Get-RequiredCheckEvidence -Repository $repo -Sha $failedE2e | Out-Null } }
    @{ Name = 'PR #140 head 964c6bb with pull-request runs only'; Expect = 'required-checks'; Test = { Get-RequiredCheckEvidence -Repository $repo -Sha $prHead | Out-Null } }
    @{ Name = 'PR #140 head 964c6bb outside master history'; Expect = 'master-history'; Test = { Assert-OnMaster -Repository $repo -Sha $prHead } }
    @{ Name = 'experiment branch 8371cef outside master history'; Expect = 'master-history'; Test = { Assert-OnMaster -Repository $repo -Sha $experiment } }
    @{ Name = 'non-existent tag v0.0.0'; Expect = 'HTTP 404'; Test = { Resolve-ReleaseTag -Repository $repo -Tag 'v0.0.0' | Out-Null } }
    @{ Name = 'invalid tag v1.0'; Expect = 'tag-format'; Test = { Resolve-ReleaseTag -Repository $repo -Tag 'v1.0' | Out-Null } }
    @{ Name = 'this branch push as a release context'; Expect = 'context'; Test = {
        Assert-ReleaseContext -Repository $repo -Context @{
            EventName = $env:GITHUB_EVENT_NAME; RefType = $env:GITHUB_REF_TYPE; Ref = $env:GITHUB_REF
            RefName = $env:GITHUB_REF_NAME; Repository = $env:GITHUB_REPOSITORY; WorkflowRef = $env:GITHUB_WORKFLOW_REF } } }
    @{ Name = 'no release or draft for v0.0.0-rc.0'; Expect = 'pass'; Test = { Assert-NoExistingRelease -Repository $repo -Tag 'v0.0.0-rc.0' } }
)

$mismatch = 0
foreach ($case in $cases) {
    $outcome = 'pass'
    try { & $case.Test } catch { $outcome = ($_.Exception.Message -replace '\s+', ' ') }
    $ok = if ($case.Expect -eq 'pass') { $outcome -eq 'pass' } else { $outcome -ne 'pass' -and $outcome -like "*$($case.Expect)*" }
    if (-not $ok) { $mismatch++ }
    $label = if ($ok) { 'ok' } else { 'MISMATCH' }
    Write-Host ("[{0}] {1} => {2}" -f $label, $case.Name, $outcome.Substring(0, [Math]::Min(240, $outcome.Length)))
}
if ($mismatch) { throw "$mismatch case(s) did not behave as expected" }
