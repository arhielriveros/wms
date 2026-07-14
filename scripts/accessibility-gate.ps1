[CmdletBinding()]
param(
    [int]$Port = 3100,
    [string]$EvidenceDirectory = ".backups/accessibility"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$web = Join-Path $root "apps/web"
$onWindows = $PSVersionTable.PSEdition -eq "Desktop" -or $env:OS -eq "Windows_NT"
$npm = if ($onWindows) { "npm.cmd" } else { "npm" }
$npx = if ($onWindows) { "npx.cmd" } else { "npx" }
$node = if ($onWindows) { "node.exe" } else { "node" }
$baseUrl = "http://127.0.0.1:$Port"
$server = $null

try {
    & $npm --workspace "@wms/supervisor-web" run build
    if ($LASTEXITCODE -ne 0) { throw "Web build failed with exit code $LASTEXITCODE." }

    New-Item -ItemType Directory -Force -Path (Join-Path $root $EvidenceDirectory) | Out-Null
    $start = @{
        FilePath = $node
        ArgumentList = @("node_modules/next/dist/bin/next", "start", "--hostname", "127.0.0.1", "--port", $Port)
        WorkingDirectory = $web
        PassThru = $true
    }
    if ($onWindows) { $start["WindowStyle"] = "Hidden" }
    $server = Start-Process @start

    $deadline = (Get-Date).AddSeconds(90)
    do {
        if ($server.HasExited) { throw "Accessibility web server exited before becoming ready." }
        try {
            $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -TimeoutSec 2
            $ready = $response.StatusCode -eq 200
        } catch {
            $ready = $false
        }
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    } until ($ready -or (Get-Date) -ge $deadline)
    if (-not $ready) { throw "Accessibility web server did not become ready within 90 seconds." }

    $env:WMS_ACCESSIBILITY_BASE_URL = $baseUrl
    $testExitCode = -1
    Push-Location $web
    try {
        & $npx playwright test --config=playwright.accessibility.config.ts
        $testExitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($testExitCode -ne 0) { throw "Accessibility gate failed with exit code $testExitCode." }
} finally {
    Remove-Item Env:WMS_ACCESSIBILITY_BASE_URL -ErrorAction SilentlyContinue
    if ($null -ne $server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        $server.WaitForExit(5000) | Out-Null
    }
}

Write-Output "Accessibility gate passed. Evidence: $EvidenceDirectory"
