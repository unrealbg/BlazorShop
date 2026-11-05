# Offline tests for the R11 release provenance guards and the release workflow policy.
# Run: pwsh ./scripts/release/tests/ReleaseGuard.Tests.ps1   (exit code 0 = all passed)
# GitHub API responses are stubbed; git and file checks use temporary directories.
# Nothing here contacts GitHub, creates a tag or creates a release.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseGuard.psm1') -Force

$script:Passed = 0
$script:Failed = @()

function It([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:Passed++; Write-Host "  [pass] $Name" }
    catch { $script:Failed += $Name; Write-Host "  [FAIL] $Name :: $($_.Exception.Message)" -ForegroundColor Red }
}

function Should-Throw([scriptblock]$Body, [string]$Rule) {
    try { & $Body } catch {
        if ($Rule -and $_.Exception.Message -notlike "*'$Rule'*") { throw "expected rule '$Rule', got: $($_.Exception.Message)" }
        return
    }
    throw "expected the guard to fail closed (rule '$Rule'), but it passed"
}

$repo = 'unrealbg/BlazorShop'
$sha = 'a' * 40
$otherSha = 'b' * 40
$tagObject = 'c' * 40

# Builds a stub API from a path -> response table; unknown paths fail like a 404.
function New-Api([hashtable]$Responses) {
    $table = $Responses
    return { param($Path) if ($table.ContainsKey($Path)) { return $table[$Path] } else { throw "HTTP 404 for $Path" } }.GetNewClosure()
}

function New-GreenChecks([string]$CommitSha, [hashtable]$Override = @{}) {
    $responses = @{
        "repos/$repo/actions/runs?head_sha=$CommitSha&event=push&branch=master&per_page=100" = [pscustomobject]@{ workflow_runs = @(
            [pscustomobject]@{ id = 1; head_sha = $CommitSha; event = 'push'; head_branch = 'master'; path = '.github/workflows/ci.yml' },
            [pscustomobject]@{ id = 2; head_sha = $CommitSha; event = 'push'; head_branch = 'master'; path = '.github/workflows/code-analysis.yml' }) }
        "repos/$repo/actions/runs/1/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @(
            [pscustomobject]@{ id = 11; name = 'build-test'; head_sha = $CommitSha; status = 'completed'; conclusion = 'success' },
            [pscustomobject]@{ id = 12; name = 'postgres16-archive'; head_sha = $CommitSha; status = 'completed'; conclusion = 'success' },
            [pscustomobject]@{ id = 13; name = 'browser-e2e'; head_sha = $CommitSha; status = 'completed'; conclusion = 'success' }) }
        "repos/$repo/actions/runs/2/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @(
            [pscustomobject]@{ id = 21; name = 'analyze'; head_sha = $CommitSha; status = 'completed'; conclusion = 'success' }) }
    }
    foreach ($pair in @(@(11, 'build-test'), @(12, 'postgres16-archive'), @(13, 'browser-e2e'), @(21, 'analyze'))) {
        $responses["repos/$repo/check-runs/$($pair[0])"] = [pscustomobject]@{
            name = $pair[1]; head_sha = $CommitSha; conclusion = 'success'; app = [pscustomobject]@{ id = 15368; slug = 'github-actions' } }
    }
    foreach ($key in $Override.Keys) { $responses[$key] = $Override[$key] }
    return $responses
}

function New-Context([string]$Tag, [hashtable]$Override = @{}) {
    $context = @{ EventName = 'push'; RefType = 'tag'; Ref = "refs/tags/$Tag"; RefName = $Tag; Repository = $repo
        WorkflowRef = "$repo/.github/workflows/release.yml@refs/tags/$Tag" }
    foreach ($key in $Override.Keys) { $context[$key] = $Override[$key] }
    return $context
}

function New-ArtifactDirectory {
    $directory = Join-Path ([IO.Path]::GetTempPath()) ("release-guard-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory | Out-Null
    $names = @('blazorshop-api-v1.0.0.tar.gz', 'blazorshop-storefront-v1.0.0.tar.gz', 'blazorshop-account-v1.0.0.tar.gz', 'provenance.json')
    foreach ($name in $names) { Set-Content -LiteralPath (Join-Path $directory $name) -Value "content of $name" -NoNewline }
    $lines = $names | Sort-Object | ForEach-Object { "$(Get-FileSha256 (Join-Path $directory $_))  $_" }
    [IO.File]::WriteAllText((Join-Path $directory 'SHA256SUMS'), ($lines -join "`n") + "`n")
    return [pscustomobject]@{ Path = $directory; Files = @($names + 'SHA256SUMS'); SumsSha = (Get-FileSha256 (Join-Path $directory 'SHA256SUMS')) }
}

Write-Host 'Tag format'
It 'accepts vMAJOR.MINOR.PATCH and -rc.N' {
    foreach ($tag in 'v0.1.0', 'v1.2.3', 'v10.20.30', 'v1.0.0-rc.1', 'v2.0.0-rc.12') { Assert-ReleaseTagFormat -Tag $tag }
}
It 'rejects invalid SemVer tags (negative)' {
    foreach ($tag in '', '1.2.3', 'v1.2', 'v1.2.3.4', 'v01.2.3', 'v1.02.3', 'v1.2.3-beta.1', 'v1.2.3-rc', 'v1.2.3-rc.01', 'v1.2.3+build.1', 'V1.2.3', 'v1.2.3 ', 'v1.2.3-RC.1') {
        Should-Throw { Assert-ReleaseTagFormat -Tag $tag } 'tag-format'
    }
}
It 'marks only -rc.N tags as prerelease' {
    if (-not (Test-PrereleaseTag 'v1.0.0-rc.2') -or (Test-PrereleaseTag 'v1.0.0')) { throw 'prerelease detection is wrong' }
}

Write-Host 'Build context'
It 'accepts a tag push of release.yml in this repository' { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0') }
It 'rejects workflow_dispatch, pull_request and schedule events (negative: unauthorized build context)' {
    foreach ($eventName in 'workflow_dispatch', 'pull_request', 'pull_request_target', 'schedule', 'release') {
        Should-Throw { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0' @{ EventName = $eventName }) } 'context'
    }
}
It 'rejects a branch ref, another repository and another workflow file (negative: unauthorized build context)' {
    Should-Throw { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0' @{ RefType = 'branch'; Ref = 'refs/heads/master' }) } 'context'
    Should-Throw { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0' @{ Repository = 'someone/fork' }) } 'context'
    Should-Throw { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0' @{ WorkflowRef = "$repo/.github/workflows/other.yml@refs/tags/v1.0.0" }) } 'context'
    Should-Throw { Assert-ReleaseContext -Repository $repo -Context (New-Context 'v1.0.0' @{ WorkflowRef = "$repo/.github/workflows/release.yml@refs/heads/master" }) } 'context'
}

Write-Host 'Tag resolution'
$lightweight = @{ "repos/$repo/git/ref/tags/v1.0.0" = [pscustomobject]@{ ref = 'refs/tags/v1.0.0'; object = [pscustomobject]@{ type = 'commit'; sha = $sha } } }
$annotated = @{
    "repos/$repo/git/ref/tags/v1.0.0" = [pscustomobject]@{ ref = 'refs/tags/v1.0.0'; object = [pscustomobject]@{ type = 'tag'; sha = $tagObject } }
    "repos/$repo/git/tags/$tagObject" = [pscustomobject]@{ tag = 'v1.0.0'; object = [pscustomobject]@{ type = 'commit'; sha = $sha } }
}
It 'resolves a lightweight tag to its commit' {
    $resolved = Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $lightweight)
    if ($resolved.CommitSha -ne $sha -or $resolved.TagObjectSha) { throw 'wrong resolution' }
}
It 'peels an annotated tag to its commit and records the tag object' {
    $resolved = Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $annotated)
    if ($resolved.CommitSha -ne $sha -or $resolved.TagObjectSha -ne $tagObject) { throw 'wrong resolution' }
    Assert-EventSha -EventSha $sha -Resolved $resolved
    Assert-EventSha -EventSha $tagObject -Resolved $resolved
}
It 'rejects a tag whose commit is not the approved/event SHA (negative)' {
    $resolved = Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $lightweight)
    Should-Throw { Assert-EventSha -EventSha $otherSha -Resolved $resolved } 'tag-resolution'
}
It 'rejects a missing tag, a tag on a tree and nested tag objects (negative)' {
    Should-Throw { Resolve-ReleaseTag -Repository $repo -Tag 'v9.9.9' -Api (New-Api $lightweight) } ''
    $tree = @{ "repos/$repo/git/ref/tags/v1.0.0" = [pscustomobject]@{ ref = 'refs/tags/v1.0.0'; object = [pscustomobject]@{ type = 'tree'; sha = $sha } } }
    Should-Throw { Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $tree) } 'tag-resolution'
    $nested = @{
        "repos/$repo/git/ref/tags/v1.0.0" = [pscustomobject]@{ ref = 'refs/tags/v1.0.0'; object = [pscustomobject]@{ type = 'tag'; sha = $tagObject } }
        "repos/$repo/git/tags/$tagObject" = [pscustomobject]@{ tag = 'v1.0.0'; object = [pscustomobject]@{ type = 'tag'; sha = $otherSha } }
    }
    Should-Throw { Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $nested) } 'tag-resolution'
    $renamed = @{
        "repos/$repo/git/ref/tags/v1.0.0" = [pscustomobject]@{ ref = 'refs/tags/v1.0.0'; object = [pscustomobject]@{ type = 'tag'; sha = $tagObject } }
        "repos/$repo/git/tags/$tagObject" = [pscustomobject]@{ tag = 'v0.9.0'; object = [pscustomobject]@{ type = 'commit'; sha = $sha } }
    }
    Should-Throw { Resolve-ReleaseTag -Repository $repo -Tag 'v1.0.0' -Api (New-Api $renamed) } 'tag-resolution'
}

Write-Host 'Master history'
It 'accepts a commit equal to or behind master' {
    foreach ($status in 'identical', 'ahead') {
        Assert-OnMaster -Repository $repo -Sha $sha -Api (New-Api @{ "repos/$repo/compare/$sha...master" = [pscustomobject]@{ status = $status; behind_by = 0 } })
    }
}
It 'rejects a SHA outside master history (negative)' {
    foreach ($comparison in @([pscustomobject]@{ status = 'diverged'; behind_by = 2 }, [pscustomobject]@{ status = 'behind'; behind_by = 1 })) {
        Should-Throw { Assert-OnMaster -Repository $repo -Sha $sha -Api (New-Api @{ "repos/$repo/compare/$sha...master" = $comparison }) } 'master-history'
    }
    Should-Throw { Assert-OnMaster -Repository $repo -Sha $sha -Api (New-Api @{}) } ''
}

Write-Host 'Required checks'
It 'collects four green GitHub Actions results for the exact commit' {
    $evidence = Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha))
    if (@($evidence).Count -ne 4 -or @($evidence | Where-Object conclusion -ne 'success').Count) { throw 'unexpected evidence' }
}
It 'rejects a SHA whose browser-e2e failed (negative)' {
    $override = @{ "repos/$repo/actions/runs/1/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @(
        [pscustomobject]@{ id = 11; name = 'build-test'; head_sha = $sha; status = 'completed'; conclusion = 'success' },
        [pscustomobject]@{ id = 12; name = 'postgres16-archive'; head_sha = $sha; status = 'completed'; conclusion = 'success' },
        [pscustomobject]@{ id = 13; name = 'browser-e2e'; head_sha = $sha; status = 'completed'; conclusion = 'failure' }) } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $override)) } 'required-checks'
}
It 'rejects a missing required check (negative)' {
    $override = @{ "repos/$repo/actions/runs/2/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @() } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $override)) } 'required-checks'
}
It 'rejects checks that exist only for pull-request runs or merge commits (negative)' {
    $prOnly = @{ "repos/$repo/actions/runs?head_sha=$sha&event=push&branch=master&per_page=100" = [pscustomobject]@{ workflow_runs = @(
        [pscustomobject]@{ id = 1; head_sha = $sha; event = 'pull_request'; head_branch = 'feature'; path = '.github/workflows/ci.yml' }) } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $prOnly)) } 'required-checks'
    $mergeSha = @{ "repos/$repo/actions/runs?head_sha=$sha&event=push&branch=master&per_page=100" = [pscustomobject]@{ workflow_runs = @(
        [pscustomobject]@{ id = 1; head_sha = $otherSha; event = 'push'; head_branch = 'master'; path = '.github/workflows/ci.yml' }) } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $mergeSha)) } 'required-checks'
}
It 'rejects in-progress, conflicting or non-GitHub-Actions results (negative)' {
    $pending = @{ "repos/$repo/actions/runs/2/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @(
        [pscustomobject]@{ id = 21; name = 'analyze'; head_sha = $sha; status = 'in_progress'; conclusion = $null }) } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $pending)) } 'required-checks'
    $duplicate = New-GreenChecks $sha @{
        "repos/$repo/actions/runs?head_sha=$sha&event=push&branch=master&per_page=100" = [pscustomobject]@{ workflow_runs = @(
            [pscustomobject]@{ id = 1; head_sha = $sha; event = 'push'; head_branch = 'master'; path = '.github/workflows/ci.yml' },
            [pscustomobject]@{ id = 2; head_sha = $sha; event = 'push'; head_branch = 'master'; path = '.github/workflows/code-analysis.yml' },
            [pscustomobject]@{ id = 3; head_sha = $sha; event = 'push'; head_branch = 'master'; path = '.github/workflows/ci.yml' }) }
        "repos/$repo/actions/runs/3/jobs?filter=latest&per_page=100" = [pscustomobject]@{ jobs = @(
            [pscustomobject]@{ id = 31; name = 'browser-e2e'; head_sha = $sha; status = 'completed'; conclusion = 'failure' }) }
    }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api $duplicate) } 'required-checks'
    $foreignApp = @{ "repos/$repo/check-runs/13" = [pscustomobject]@{ name = 'browser-e2e'; head_sha = $sha; conclusion = 'success'; app = [pscustomobject]@{ id = 999; slug = 'third-party' } } }
    Should-Throw { Get-RequiredCheckEvidence -Repository $repo -Sha $sha -Api (New-Api (New-GreenChecks $sha $foreignApp)) } 'required-checks'
}

Write-Host 'Checkout HEAD'
$gitDirectory = Join-Path ([IO.Path]::GetTempPath()) ("release-guard-git-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $gitDirectory | Out-Null
& git -C $gitDirectory init -q
& git -C $gitDirectory -c user.name=test -c user.email=test@example.invalid commit -q --allow-empty -m one
$first = (& git -C $gitDirectory rev-parse HEAD).Trim()
& git -C $gitDirectory -c user.name=test -c user.email=test@example.invalid commit -q --allow-empty -m two
It 'accepts a checkout whose HEAD is the approved commit' { Assert-CheckoutHead -ExpectedSha ((& git -C $gitDirectory rev-parse HEAD).Trim()) -Path $gitDirectory }
It 'rejects a tag / checkout HEAD mismatch (negative)' { Should-Throw { Assert-CheckoutHead -ExpectedSha $first -Path $gitDirectory } 'checkout-head' }
Remove-Item -Recurse -Force $gitDirectory

Write-Host 'Artifact integrity'
It 'accepts an intact artifact set' {
    $set = New-ArtifactDirectory
    Assert-ArtifactSet -Directory $set.Path -ExpectedSumsSha256 $set.SumsSha -ExpectedFiles $set.Files
    Remove-Item -Recurse -Force $set.Path
}
It 'rejects a modified archive (negative: SHA-256 mismatch)' {
    $set = New-ArtifactDirectory
    Add-Content -LiteralPath (Join-Path $set.Path 'blazorshop-api-v1.0.0.tar.gz') -Value 'tampered'
    Should-Throw { Assert-ArtifactSet -Directory $set.Path -ExpectedSumsSha256 $set.SumsSha -ExpectedFiles $set.Files } 'artifact-integrity'
    Remove-Item -Recurse -Force $set.Path
}
It 'rejects a replaced SHA256SUMS, an extra file and a missing file (negative)' {
    $set = New-ArtifactDirectory
    Add-Content -LiteralPath (Join-Path $set.Path 'SHA256SUMS') -Value ''
    Should-Throw { Assert-ArtifactSet -Directory $set.Path -ExpectedSumsSha256 $set.SumsSha -ExpectedFiles $set.Files } 'artifact-integrity'
    Remove-Item -Recurse -Force $set.Path
    $set = New-ArtifactDirectory
    Set-Content -LiteralPath (Join-Path $set.Path 'extra.tar.gz') -Value 'x'
    Should-Throw { Assert-ArtifactSet -Directory $set.Path -ExpectedSumsSha256 $set.SumsSha -ExpectedFiles $set.Files } 'artifact-integrity'
    Remove-Item -Recurse -Force $set.Path
    $set = New-ArtifactDirectory
    Remove-Item -LiteralPath (Join-Path $set.Path 'provenance.json')
    Should-Throw { Assert-ArtifactSet -Directory $set.Path -ExpectedSumsSha256 $set.SumsSha -ExpectedFiles $set.Files } 'artifact-integrity'
    Remove-Item -Recurse -Force $set.Path
}

Write-Host 'Attestations'
It 'passes the strict verification arguments for every file' {
    $calls = [System.Collections.Generic.List[string]]::new()
    $verify = { param($File, $Arguments) $calls.Add("$([IO.Path]::GetFileName($File)) $($Arguments -join ' ')"); return 0 }.GetNewClosure()
    Assert-AttestationsVerified -Repository $repo -Tag 'v1.0.0' -Sha $sha -Files @('a.tar.gz', 'provenance.json') -Verify $verify
    $expected = "--repo $repo --signer-workflow $repo/.github/workflows/release.yml --source-ref refs/tags/v1.0.0 --source-digest $sha --deny-self-hosted-runners"
    if ($calls.Count -ne 2 -or $calls[0] -ne "a.tar.gz $expected") { throw "unexpected verification call: $($calls[0])" }
}
It 'rejects an artifact without a valid attestation (negative: unauthorized build context)' {
    Should-Throw { Assert-AttestationsVerified -Repository $repo -Tag 'v1.0.0' -Sha $sha -Files @('a.tar.gz') -Verify { param($File, $Arguments) return 1 } } 'attestation'
}

Write-Host 'Release immutability'
It 'accepts a tag without any release' {
    Assert-NoExistingRelease -Repository $repo -Tag 'v1.0.0' -Api (New-Api @{ "repos/$repo/releases?per_page=100&page=1" = @([pscustomobject]@{ id = 1; tag_name = 'v0.9.0'; draft = $false }) })
}
It 'rejects republishing over a published release or an existing draft (negative)' {
    foreach ($draft in $false, $true) {
        $api = New-Api @{ "repos/$repo/releases?per_page=100&page=1" = @([pscustomobject]@{ id = 7; tag_name = 'v1.0.0'; draft = $draft }) }
        Should-Throw { Assert-NoExistingRelease -Repository $repo -Tag 'v1.0.0' -Api $api } 'release-immutability'
    }
}

Write-Host 'Workflow policy (.github/workflows/release.yml)'
$workflowPath = Join-Path $PSScriptRoot '..' '..' '..' '.github' 'workflows' 'release.yml'
$workflow = Get-Content -LiteralPath $workflowPath -Raw
function Get-JobBlock([string]$Job) {
    $match = [regex]::Match($workflow, "(?ms)^  ${Job}:\r?\n(.*?)(?=^  [A-Za-z0-9_-]+:\r?\n|\z)")
    if (-not $match.Success) { throw "job '$Job' not found" }
    return $match.Groups[1].Value
}
It 'is triggered only by v* tag pushes (no workflow_dispatch, no release event)' {
    if ($workflow -match 'workflow_dispatch' -or $workflow -match '(?m)^\s+release:\s*$' -or $workflow -notmatch '(?ms)^on:\s*\r?\n\s+push:\s*\r?\n\s+tags:\s*\r?\n\s+- "v\*"') { throw 'unexpected triggers' }
}
It 'never overwrites release assets (no --clobber) and creates drafts only' {
    if ($workflow -match '--clobber') { throw '--clobber present' }
    $publish = Get-JobBlock 'publish-draft'
    if ($publish -notmatch 'gh release create' -or $publish -notmatch '--draft' -or $publish -notmatch '--verify-tag') { throw 'draft release creation not enforced' }
    if ($workflow -match 'gh release (upload|edit|delete)') { throw 'release mutation command present' }
}
It 'grants no workflow-level permissions and least privilege per job' {
    if ($workflow -notmatch '(?m)^permissions: \{\}\s*$') { throw 'workflow-level permissions are not empty' }
    $expected = @{
        'validate-build' = @('actions: read', 'checks: read', 'contents: read')
        'attest' = @('attestations: write', 'contents: read', 'id-token: write')
        'publish-draft' = @('attestations: read', 'contents: write')
    }
    foreach ($job in $expected.Keys) {
        $block = Get-JobBlock $job
        $permissions = [regex]::Match($block, '(?ms)^    permissions:\r?\n((?:      [a-z-]+: [a-z]+\r?\n)+)')
        if (-not $permissions.Success) { throw "job '$job' has no explicit permissions" }
        $actual = @($permissions.Groups[1].Value -split '\r?\n' | ForEach-Object Trim | Where-Object { $_ } | Sort-Object)
        if (($actual -join ',') -ne (($expected[$job] | Sort-Object) -join ',')) { throw "job '$job' permissions are [$($actual -join ', ')]" }
    }
    if ((Get-JobBlock 'validate-build') -match '(?m)^\s+[a-z-]+:\s*write\s*$') { throw 'build job has a write permission' }
}
It 'does not persist checkout credentials and builds nothing in the write job' {
    $checkouts = [regex]::Matches($workflow, '(?ms)uses: actions/checkout@v\d+\r?\n\s+with:\r?\n(.*?)(?=^\s+- name:|\z)')
    if ($checkouts.Count -lt 3) { throw "expected a checkout in each job, found $($checkouts.Count)" }
    foreach ($checkout in $checkouts) { if ($checkout.Groups[1].Value -notmatch 'persist-credentials: false') { throw 'a checkout persists credentials' } }
    foreach ($job in 'attest', 'publish-draft') {
        if ((Get-JobBlock $job) -match '(?m)\b(npm|npx|dotnet|setup-node|setup-dotnet|msbuild)\b') { throw "job '$job' runs build tooling" }
        if ((Get-JobBlock $job) -notmatch 'sparse-checkout') { throw "job '$job' checks out more than the release scripts" }
    }
}
It 'does not override the attested source ref (dry-run hook unused)' {
    if ($workflow -match 'RELEASE_GUARD_SOURCE_REF') { throw 'release.yml overrides the attestation source ref' }
}
It 'serialises runs per tag and gates publishing on this repository' {
    if ($workflow -notmatch '(?ms)^concurrency:\r?\n\s+group: release-\$\{\{ github\.ref \}\}\r?\n\s+cancel-in-progress: false') { throw 'missing per-tag concurrency group' }
    if ((Get-JobBlock 'publish-draft') -notmatch "github\.repository == 'unrealbg/BlazorShop'") { throw 'publish job not limited to this repository' }
}

Write-Host ''
Write-Host "Passed: $($script:Passed)  Failed: $($script:Failed.Count)"
if ($script:Failed.Count -gt 0) { $script:Failed | ForEach-Object { Write-Host "  - $_" }; exit 1 }
exit 0
