# Release provenance guards (R11). Every function throws on the first violated rule, so the
# release workflow fails closed before anything is built or published. GitHub API access is
# injected through -Api (a scriptblock taking a relative API path and returning parsed JSON),
# so the rules can be tested without network access.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:GitHubActionsAppId = 15368
$script:GitHubActionsAppSlug = 'github-actions'
$script:ReleaseWorkflowPath = '.github/workflows/release.yml'
$script:RequiredChecks = @('build-test', 'postgres16-archive', 'browser-e2e', 'analyze')
$script:CheckWorkflows = @('.github/workflows/ci.yml', '.github/workflows/code-analysis.yml')
$script:TagPattern = '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc\.(0|[1-9]\d*))?$'
$script:ShaPattern = '^[0-9a-f]{40}$'

function Invoke-GitHubApi {
    param([Parameter(Mandatory)][string]$Path)
    $output = & gh api -H 'Accept: application/vnd.github+json' $Path 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed for '$Path': $($output | Out-String)"
    }
    return ($output | Out-String | ConvertFrom-Json -Depth 32)
}

function Get-DefaultApi {
    return { param($Path) Invoke-GitHubApi -Path $Path }
}

function Fail([string]$Rule, [string]$Message) {
    throw "Release guard '$Rule' failed: $Message"
}

function Assert-ReleaseTagFormat {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Tag)
    if ($Tag -cnotmatch $script:TagPattern) {
        Fail 'tag-format' "'$Tag' is not vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rc.N."
    }
}

function Test-PrereleaseTag {
    param([Parameter(Mandatory)][string]$Tag)
    return $Tag -cmatch '-rc\.'
}

function Assert-ReleaseContext {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][hashtable]$Context
    )

    $tag = [string]$Context.RefName
    if ($Context.EventName -ne 'push') { Fail 'context' "event '$($Context.EventName)' is not a tag push." }
    if ($Context.RefType -ne 'tag') { Fail 'context' "ref type '$($Context.RefType)' is not a tag." }
    if ($Context.Ref -ne "refs/tags/$tag") { Fail 'context' "ref '$($Context.Ref)' does not match tag '$tag'." }
    if ($Context.Repository -ne $Repository) { Fail 'context' "repository '$($Context.Repository)' is not '$Repository'." }
    $expectedWorkflow = "$Repository/$($script:ReleaseWorkflowPath)@refs/tags/$tag"
    if ($Context.WorkflowRef -ne $expectedWorkflow) {
        Fail 'context' "workflow '$($Context.WorkflowRef)' is not '$expectedWorkflow'."
    }
    Assert-ReleaseTagFormat -Tag $tag
}

function Resolve-ReleaseTag {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Tag,
        [scriptblock]$Api = (Get-DefaultApi)
    )

    Assert-ReleaseTagFormat -Tag $Tag
    $reference = & $Api "repos/$Repository/git/ref/tags/$Tag"
    if ($null -eq $reference -or $reference -is [array] -or $reference.ref -ne "refs/tags/$Tag") {
        Fail 'tag-resolution' "tag '$Tag' does not resolve to exactly one reference."
    }

    $object = $reference.object
    $tagObjectSha = $null
    for ($depth = 0; $object.type -eq 'tag'; $depth++) {
        if ($depth -ge 1) { Fail 'tag-resolution' "tag '$Tag' points to another tag object; nested tags are not accepted." }
        $tagObjectSha = [string]$object.sha
        $annotated = & $Api "repos/$Repository/git/tags/$tagObjectSha"
        if ($annotated.tag -ne $Tag) { Fail 'tag-resolution' "annotated tag object names '$($annotated.tag)', not '$Tag'." }
        $object = $annotated.object
    }

    if ($object.type -ne 'commit' -or [string]$object.sha -notmatch $script:ShaPattern) {
        Fail 'tag-resolution' "tag '$Tag' does not point to a commit."
    }

    return [pscustomobject]@{ Tag = $Tag; CommitSha = [string]$object.sha; TagObjectSha = $tagObjectSha }
}

function Assert-EventSha {
    param(
        [Parameter(Mandatory)][string]$EventSha,
        [Parameter(Mandatory)]$Resolved
    )
    if ($EventSha -ne $Resolved.CommitSha -and $EventSha -ne $Resolved.TagObjectSha) {
        Fail 'tag-resolution' "event SHA $EventSha matches neither commit $($Resolved.CommitSha) nor tag object $($Resolved.TagObjectSha)."
    }
}

function Assert-OnMaster {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Sha,
        [scriptblock]$Api = (Get-DefaultApi)
    )
    $comparison = & $Api "repos/$Repository/compare/$Sha...master"
    if ($comparison.status -notin @('identical', 'ahead') -or [int]$comparison.behind_by -ne 0) {
        Fail 'master-history' "commit $Sha is not in the history of master (compare status '$($comparison.status)', behind_by $($comparison.behind_by))."
    }
}

function Get-RequiredCheckEvidence {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Sha,
        [scriptblock]$Api = (Get-DefaultApi)
    )

    # Only push runs on master for this exact commit count. Pull-request runs (synthetic merge
    # commits or PR heads) never qualify, even if their job names match.
    $runs = @((& $Api "repos/$Repository/actions/runs?head_sha=$Sha&event=push&branch=master&per_page=100").workflow_runs |
        Where-Object { $_.head_sha -eq $Sha -and $_.event -eq 'push' -and $_.head_branch -eq 'master' -and $_.path -in $script:CheckWorkflows })

    $jobs = @(foreach ($run in $runs) {
        foreach ($job in @((& $Api "repos/$Repository/actions/runs/$($run.id)/jobs?filter=latest&per_page=100").jobs)) {
            if ($job.name -in $script:RequiredChecks) {
                [pscustomobject]@{ Name = $job.name; Id = $job.id; RunId = $run.id; Workflow = $run.path; HeadSha = $job.head_sha; Status = $job.status; Conclusion = $job.conclusion }
            }
        }
    })

    $evidence = foreach ($name in $script:RequiredChecks) {
        $matching = @($jobs | Where-Object Name -eq $name)
        if ($matching.Count -eq 0) { Fail 'required-checks' "no '$name' result from a push run on master for $Sha." }
        foreach ($job in $matching) {
            if ($job.HeadSha -ne $Sha) { Fail 'required-checks' "'$name' job $($job.Id) ran on $($job.HeadSha), not $Sha." }
            if ($job.Status -ne 'completed' -or $job.Conclusion -ne 'success') {
                Fail 'required-checks' "'$name' job $($job.Id) is $($job.Status)/$($job.Conclusion), not completed/success."
            }
            $checkRun = & $Api "repos/$Repository/check-runs/$($job.Id)"
            if ($checkRun.app.id -ne $script:GitHubActionsAppId -or $checkRun.app.slug -ne $script:GitHubActionsAppSlug) {
                Fail 'required-checks' "'$name' check run $($job.Id) was not created by GitHub Actions."
            }
            if ($checkRun.head_sha -ne $Sha -or $checkRun.name -ne $name -or $checkRun.conclusion -ne 'success') {
                Fail 'required-checks' "'$name' check run $($job.Id) does not confirm success on $Sha."
            }
            [pscustomobject]@{ name = $name; check_run_id = [long]$job.Id; workflow_run_id = [long]$job.RunId; workflow = $job.Workflow; conclusion = 'success' }
        }
    }

    return @($evidence)
}

function Assert-CheckoutHead {
    param([Parameter(Mandatory)][string]$ExpectedSha, [string]$Path = '.')
    $head = (& git -C $Path rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $ExpectedSha) {
        Fail 'checkout-head' "checked-out HEAD '$head' is not the approved commit $ExpectedSha."
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Assert-ArtifactSet {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$ExpectedSumsSha256,
        [Parameter(Mandatory)][string[]]$ExpectedFiles
    )

    $sumsPath = Join-Path $Directory 'SHA256SUMS'
    if (-not (Test-Path -LiteralPath $sumsPath -PathType Leaf)) { Fail 'artifact-integrity' 'SHA256SUMS is missing.' }
    if ((Get-FileSha256 $sumsPath) -ne $ExpectedSumsSha256.ToLowerInvariant()) {
        Fail 'artifact-integrity' 'SHA256SUMS differs from the manifest produced by the build job.'
    }

    $present = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/') } | Sort-Object)
    $expected = @(@($ExpectedFiles) + 'SHA256SUMS' | Sort-Object -Unique)
    if (($present -join "`n") -cne ($expected -join "`n")) {
        Fail 'artifact-integrity' "artifact files [$($present -join ', ')] differ from the expected set [$($expected -join ', ')]."
    }

    $listed = @{}
    foreach ($line in Get-Content -LiteralPath $sumsPath) {
        if ($line -notmatch '^([0-9a-f]{64})  ([A-Za-z0-9._-]+)$') { Fail 'artifact-integrity' "malformed SHA256SUMS line '$line'." }
        if ($listed.ContainsKey($Matches[2])) { Fail 'artifact-integrity' "SHA256SUMS lists '$($Matches[2])' twice." }
        $listed[$Matches[2]] = $Matches[1]
    }

    $covered = @($expected | Where-Object { $_ -ne 'SHA256SUMS' })
    if ((@($listed.Keys | Sort-Object) -join "`n") -cne ($covered -join "`n")) { Fail 'artifact-integrity' 'SHA256SUMS does not list exactly the release files.' }
    foreach ($name in $covered) {
        if ((Get-FileSha256 (Join-Path $Directory $name)) -ne $listed[$name]) { Fail 'artifact-integrity' "SHA-256 mismatch for '$name'." }
    }
}

function Assert-AttestationsVerified {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Tag,
        [Parameter(Mandatory)][string]$Sha,
        [Parameter(Mandatory)][string[]]$Files,
        [scriptblock]$Verify = { param($File, $Arguments) & gh attestation verify $File @Arguments 2>&1 | Out-Host; return $LASTEXITCODE },
        [string]$SourceRef = "refs/tags/$Tag"
    )
    $arguments = @(
        '--repo', $Repository,
        '--signer-workflow', "$Repository/$($script:ReleaseWorkflowPath)",
        '--source-ref', $SourceRef,
        '--source-digest', $Sha,
        '--deny-self-hosted-runners'
    )
    foreach ($file in $Files) {
        $exitCode = & $Verify $file $arguments
        if ($exitCode -ne 0) { Fail 'attestation' "attestation for '$([IO.Path]::GetFileName($file))' could not be verified." }
    }
}

function Assert-NoExistingRelease {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Tag,
        [scriptblock]$Api = (Get-DefaultApi)
    )
    # The list endpoint includes drafts (get-by-tag does not), so a retry never reuses a draft.
    for ($page = 1; ; $page++) {
        $releases = @(& $Api "repos/$Repository/releases?per_page=100&page=$page")
        if ($releases.Count -eq 0) { break }
        $existing = @($releases | Where-Object { $_.tag_name -eq $Tag })
        if ($existing.Count -gt 0) {
            Fail 'release-immutability' "a release for '$Tag' already exists (id $($existing[0].id), draft=$($existing[0].draft)); nothing is replaced."
        }
        if ($releases.Count -lt 100) { break }
    }
}

Export-ModuleMember -Function Assert-ReleaseTagFormat, Test-PrereleaseTag, Assert-ReleaseContext, Resolve-ReleaseTag,
    Assert-EventSha, Assert-OnMaster, Get-RequiredCheckEvidence, Assert-CheckoutHead, Get-FileSha256,
    Assert-ArtifactSet, Assert-AttestationsVerified, Assert-NoExistingRelease
