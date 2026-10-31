param([Parameter(Mandatory = $true)][string]$Path)

$ErrorActionPreference = 'Stop'
[xml]$trx = Get-Content -LiteralPath $Path -Raw
$namespace = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
$namespace.AddNamespace('trx', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$results = @($trx.SelectNodes('//trx:UnitTestResult', $namespace))
$required = @{
    'RefreshRotationHttpTests' = 14
    'RefreshEligibilityHttpTests' = 17
    'SessionObservationHttpTests' = 18
}
foreach ($group in $required.Keys) {
    $matches = @($results | Where-Object { $_.testName.StartsWith("BlazorShop.Tests.Infrastructure.PostgreSql.$group.") })
    if ($matches.Count -ne $required[$group] -or @($matches | Where-Object { $_.outcome -ne 'Passed' }).Count -gt 0) {
        throw "Expected $($required[$group]) passing $group cases, found $($matches.Count)."
    }
    Write-Host "Verified $($matches.Count) passing $group cases."
}
