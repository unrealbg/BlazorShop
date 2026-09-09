param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = "Stop"
$expectedTest = "BlazorShop.StagingRehearsal.UpgradedDatabaseBrowserTests.UpgradedHistoricalDatabase_CheckoutStartBrowserFlow"

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Staging rehearsal TRX report was not created: $Path"
}

[xml] $report = Get-Content -LiteralPath $Path -Raw
$namespace = New-Object System.Xml.XmlNamespaceManager($report.NameTable)
$namespace.AddNamespace('trx', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$results = @($report.SelectNodes('//trx:UnitTestResult', $namespace))
if ($results.Count -eq 0) {
    throw 'The staging rehearsal discovered zero browser tests.'
}

$failed = @($results | Where-Object { $_.outcome -ne 'Passed' })
if ($failed.Count -gt 0) {
    throw "The staging rehearsal contains $($failed.Count) non-passing browser test result(s)."
}

$executedTests = @($results | ForEach-Object { [string] $_.testName } | Sort-Object -Unique)
if ($results.Count -ne 1 -or $executedTests.Count -ne 1 -or $executedTests[0] -ne $expectedTest) {
    throw "The mandatory upgraded-database browser scenario was not the exact executed test."
}

Write-Host "Verified the mandatory upgraded-database browser scenario in $Path."
