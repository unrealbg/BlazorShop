# Entry point used by .github/workflows/release.yml. Each command enforces one stage of the
# release provenance rules (docs/specs/r11-release-provenance.md) and exits non-zero on the
# first violation. No command creates, edits or deletes a tag or release.
param(
    [Parameter(Mandatory)]
    [ValidateSet('VerifySource', 'WriteManifest', 'VerifyArtifacts', 'VerifyAttestations', 'AssertTagUnchanged', 'AssertNoRelease')]
    [string]$Command,
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$Tag = $env:GITHUB_REF_NAME,
    [string]$Sha,
    [string]$Directory,
    [string]$ExpectedSumsSha256,
    [string]$EvidencePath,
    [string[]]$ReleaseFiles
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseGuard.psm1') -Force

function Write-StepOutput([string]$Name, [string]$Value) {
    if ($env:GITHUB_OUTPUT) { "$Name=$Value" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
    Write-Host "$Name=$Value"
}

function Assert-Value([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "Release guard: '$Name' is required for $Command." }
}

Assert-Value 'Repository' $Repository

switch ($Command) {
    'VerifySource' {
        Assert-Value 'EvidencePath' $EvidencePath
        Assert-ReleaseContext -Repository $Repository -Context @{
            EventName = $env:GITHUB_EVENT_NAME; RefType = $env:GITHUB_REF_TYPE; Ref = $env:GITHUB_REF
            RefName = $env:GITHUB_REF_NAME; Repository = $env:GITHUB_REPOSITORY; WorkflowRef = $env:GITHUB_WORKFLOW_REF
        }
        $resolved = Resolve-ReleaseTag -Repository $Repository -Tag $Tag
        Assert-EventSha -EventSha $env:GITHUB_SHA -Resolved $resolved
        Assert-OnMaster -Repository $Repository -Sha $resolved.CommitSha
        $evidence = Get-RequiredCheckEvidence -Repository $Repository -Sha $resolved.CommitSha
        Assert-CheckoutHead -ExpectedSha $resolved.CommitSha
        ConvertTo-Json -InputObject @($evidence) -Depth 5 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
        Write-StepOutput 'tag' $resolved.Tag
        Write-StepOutput 'sha' $resolved.CommitSha
        Write-StepOutput 'tag-object-sha' ([string]$resolved.TagObjectSha)
        Write-StepOutput 'prerelease' ((Test-PrereleaseTag -Tag $resolved.Tag).ToString().ToLowerInvariant())
        Write-Host "Source verified: $($resolved.Tag) -> $($resolved.CommitSha), on master, $(@($evidence).Count) required check results green."
    }

    'WriteManifest' {
        foreach ($pair in @(@('Directory', $Directory), @('Tag', $Tag), @('Sha', $Sha), @('EvidencePath', $EvidencePath))) { Assert-Value $pair[0] $pair[1] }
        $archives = @(Get-ChildItem -LiteralPath $Directory -File -Filter '*.tar.gz' | Sort-Object Name)
        if ($archives.Count -ne 3) { throw "Release guard: expected 3 release archives, found $($archives.Count)." }
        $provenance = [ordered]@{
            schema = 'blazorshop-release-provenance/v1'
            repository = $Repository
            tag = $Tag
            tag_object_sha = if ($env:RELEASE_TAG_OBJECT_SHA) { $env:RELEASE_TAG_OBJECT_SHA } else { $null }
            commit_sha = $Sha
            workflow = [ordered]@{ ref = $env:GITHUB_WORKFLOW_REF; run_id = $env:GITHUB_RUN_ID; run_attempt = $env:GITHUB_RUN_ATTEMPT; runner_os = $env:RUNNER_OS }
            toolchain = [ordered]@{
                dotnet_sdk = (& dotnet --version).Trim()
                node = (& node --version).Trim()
                npm = (& npm --version).Trim()
            }
            required_checks = @(Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json)
            artifacts = @($archives | ForEach-Object { [ordered]@{ name = $_.Name; sha256 = (Get-FileSha256 $_.FullName); size = $_.Length } })
            reproducible_build = $false
            built_at_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        }
        $provenancePath = Join-Path $Directory 'provenance.json'
        ConvertTo-Json -InputObject $provenance -Depth 8 | Set-Content -LiteralPath $provenancePath -Encoding utf8
        $lines = @(@($archives.Name) + 'provenance.json' | Sort-Object | ForEach-Object { "$(Get-FileSha256 (Join-Path $Directory $_))  $_" })
        $sumsPath = Join-Path $Directory 'SHA256SUMS'
        [IO.File]::WriteAllText($sumsPath, ($lines -join "`n") + "`n")
        Write-StepOutput 'sums-sha256' (Get-FileSha256 $sumsPath)
        Write-StepOutput 'release-files' ((@($archives.Name) + 'provenance.json' + 'SHA256SUMS') -join ',')
    }

    'VerifyArtifacts' {
        foreach ($pair in @(@('Directory', $Directory), @('ExpectedSumsSha256', $ExpectedSumsSha256))) { Assert-Value $pair[0] $pair[1] }
        $files = @($ReleaseFiles | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
        if ($files.Count -eq 0) { throw 'Release guard: ReleaseFiles is required for VerifyArtifacts.' }
        Assert-ArtifactSet -Directory $Directory -ExpectedSumsSha256 $ExpectedSumsSha256 -ExpectedFiles $files
        Write-Host "Artifacts verified: $($files.Count) files match SHA256SUMS ($ExpectedSumsSha256)."
    }

    'VerifyAttestations' {
        foreach ($pair in @(@('Directory', $Directory), @('Tag', $Tag), @('Sha', $Sha))) { Assert-Value $pair[0] $pair[1] }
        $subjects = @(Get-ChildItem -LiteralPath $Directory -File | Where-Object { $_.Name -like '*.tar.gz' -or $_.Name -eq 'provenance.json' } | ForEach-Object FullName)
        if ($subjects.Count -ne 4) { throw "Release guard: expected 4 attested files, found $($subjects.Count)." }
        $sourceRef = if ($env:RELEASE_GUARD_SOURCE_REF) { $env:RELEASE_GUARD_SOURCE_REF } else { "refs/tags/$Tag" }
        Assert-AttestationsVerified -Repository $Repository -Tag $Tag -Sha $Sha -Files $subjects -SourceRef $sourceRef
        Write-Host "Attestations verified for $($subjects.Count) files (signer $Repository/.github/workflows/release.yml, source $sourceRef @ $Sha)."
    }

    'AssertTagUnchanged' {
        foreach ($pair in @(@('Tag', $Tag), @('Sha', $Sha))) { Assert-Value $pair[0] $pair[1] }
        $resolved = Resolve-ReleaseTag -Repository $Repository -Tag $Tag
        if ($resolved.CommitSha -ne $Sha) { throw "Release guard 'tag-resolution' failed: tag '$Tag' now points to $($resolved.CommitSha), not the built commit $Sha." }
        Write-Host "Tag $Tag still points to $Sha."
    }

    'AssertNoRelease' {
        Assert-Value 'Tag' $Tag
        Assert-NoExistingRelease -Repository $Repository -Tag $Tag
        Write-Host "No release (published or draft) exists for $Tag."
    }
}
