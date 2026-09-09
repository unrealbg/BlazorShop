param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [ValidateRange(1, 5)]
    [int]$MaximumAttempts = 3
)

$ErrorActionPreference = 'Stop'
$disabledAptSources = [System.Collections.Generic.List[object]]::new()
$installed = $false

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw "The generated Playwright installer was not found: $InstallerPath"
}

try {
    if ($IsLinux -and (Test-Path -LiteralPath '/etc/apt/sources.list.d' -PathType Container)) {
        $thirdPartySources = Get-ChildItem -LiteralPath '/etc/apt/sources.list.d' -File |
            Where-Object {
                Select-String -LiteralPath $_.FullName -Pattern 'dl.google.com/linux/chrome-stable/deb' -SimpleMatch -Quiet
            }

        foreach ($source in $thirdPartySources) {
            $disabledPath = "$($source.FullName).blazorshop-e2e-disabled"
            & sudo mv -- $source.FullName $disabledPath
            if ($LASTEXITCODE -ne 0) {
                throw "Unable to temporarily disable the unrelated apt source $($source.FullName)."
            }

            $disabledAptSources.Add([pscustomobject]@{
                Original = $source.FullName
                Disabled = $disabledPath
            })
        }
    }

    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        Write-Host "Installing Playwright Chromium (attempt $attempt of $MaximumAttempts)."
        & pwsh -NoProfile -File $InstallerPath install --with-deps chromium

        if ($LASTEXITCODE -eq 0) {
            $installed = $true
            break
        }

        if ($attempt -lt $MaximumAttempts) {
            Start-Sleep -Seconds (5 * $attempt)
        }
    }
}
finally {
    foreach ($source in $disabledAptSources) {
        & sudo mv -- $source.Disabled $source.Original
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Unable to restore apt source $($source.Original)."
        }
    }
}

if (-not $installed) {
    throw "Playwright Chromium installation failed after $MaximumAttempts attempts."
}
