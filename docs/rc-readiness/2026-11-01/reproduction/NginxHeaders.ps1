param([string]$OutputDirectory = 'artifacts/rc128-reproduction')

$ErrorActionPreference = 'Stop'
if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^(npipe|unix)://') {
    throw 'A remote DOCKER_HOST is not permitted for this reproduction.'
}
$endpoint = docker context inspect --format '{{.Endpoints.docker.Host}}'
if ($LASTEXITCODE -ne 0 -or $endpoint -notmatch '^(npipe|unix)://') {
    throw 'This reproduction requires a local Docker socket; remote Docker endpoints are not allowed.'
}
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$fixture = [IO.Path]::GetFullPath((Join-Path $OutputDirectory 'nginx'))
New-Item -ItemType Directory -Force (Join-Path $fixture 'html') | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'BlazorShop.Presentation/BlazorShop.Web/nginx.conf') -Destination (Join-Path $fixture 'default.conf.template')
Copy-Item -LiteralPath (Join-Path $repository 'BlazorShop.Presentation/BlazorShop.Web/wwwroot/index.html') -Destination (Join-Path $fixture 'html/index.html')
Set-Content -LiteralPath (Join-Path $fixture 'html/plain.txt') -Value 'synthetic audit control'
Set-Content -LiteralPath (Join-Path $fixture 'html/audit.css') -Value '.audit { color: black; }'

$container = docker run --detach --rm --publish 127.0.0.1::80 --add-host api:127.0.0.1 `
    --env BLAZORSHOP_STOREFRONT_BASE_URL=https://shop.example.test/ `
    --mount "type=bind,source=$fixture/default.conf.template,target=/etc/nginx/templates/default.conf.template,readonly" `
    --mount "type=bind,source=$fixture/html,target=/usr/share/nginx/html,readonly" nginx:1.27.5-alpine3.21
if ($LASTEXITCODE -ne 0 -or $container -notmatch '^[a-f0-9]{64}$') {
    throw 'The disposable fixture failed to start.'
}
try {
    $binding = docker port $container 80/tcp
    $ready = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        try {
            $null = Invoke-WebRequest "http://$binding/plain.txt" -TimeoutSec 2
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 300 }
    }
    if (-not $ready) { throw 'The disposable fixture was not ready.' }
    $observations = foreach ($route in @('/plain.txt', '/', '/index.html', '/account/profile', '/audit.css')) {
        $response = Invoke-WebRequest "http://$binding$route" -TimeoutSec 5
        [pscustomobject]@{
            Route = $route
            Status = [int]$response.StatusCode
            FrameOptions = [string]$response.Headers['X-Frame-Options']
            ContentTypeOptions = [string]$response.Headers['X-Content-Type-Options']
            ReferrerPolicy = [string]$response.Headers['Referrer-Policy']
            PermissionsPolicy = [string]$response.Headers['Permissions-Policy']
            Csp = [string]$response.Headers['Content-Security-Policy']
            Cache = [string]$response.Headers['Cache-Control']
        }
    }
    $observations | ConvertTo-Json | Set-Content (Join-Path $fixture 'headers.json')
    $observations | Format-Table Route, Status, FrameOptions, ContentTypeOptions, Cache
} finally {
    docker rm --force $container | Out-Null
}
