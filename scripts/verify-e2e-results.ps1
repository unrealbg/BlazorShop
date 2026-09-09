param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

$expectedTests = @(
    'BlazorShop.E2E.StorefrontCheckoutTests.HomeCategoryAndProductNavigation_ShowsSeededCatalogContent',
    'BlazorShop.E2E.StorefrontCheckoutTests.Cart_AddsProductAndVariantAndPersistsQuantityAfterReload',
    'BlazorShop.E2E.StorefrontCheckoutTests.CheckoutStart_AnonymousLoginThenAuthenticatedCheckoutPreservesCart'
)

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "E2E TRX report was not created: $Path"
}

[xml]$trx = Get-Content -LiteralPath $Path -Raw
$namespace = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
$namespace.AddNamespace('trx', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

$results = @($trx.SelectNodes('//trx:UnitTestResult', $namespace))
if ($results.Count -eq 0) {
    throw 'The E2E run discovered zero tests.'
}

$failed = @($results | Where-Object { $_.outcome -ne 'Passed' })
if ($failed.Count -gt 0) {
    throw "The E2E report contains $($failed.Count) non-passing test result(s)."
}

$actualTests = @($results | ForEach-Object { [string]$_.testName } | Sort-Object -Unique)
$missingTests = @($expectedTests | Where-Object { $_ -notin $actualTests })
$unexpectedTests = @($actualTests | Where-Object { $_ -notin $expectedTests })

if ($missingTests.Count -gt 0) {
    throw "Mandatory E2E scenarios are missing: $($missingTests -join ', ')"
}

if ($unexpectedTests.Count -gt 0 -or $actualTests.Count -ne $expectedTests.Count) {
    throw "Expected exactly $($expectedTests.Count) E2E scenarios, found $($actualTests.Count): $($actualTests -join ', ')"
}

Write-Host "Verified $($actualTests.Count) mandatory passing E2E scenarios in $Path."
