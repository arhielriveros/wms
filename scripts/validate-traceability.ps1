[CmdletBinding()]
param([string]$DocsPath = "docs")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$docs = (Resolve-Path (Join-Path $root $DocsPath)).Path
$matrix = Join-Path $docs "traceability/requirements-traceability-matrix.md"
if (-not (Test-Path -LiteralPath $matrix)) { throw "Traceability matrix is missing." }

$all = (Get-ChildItem $docs -Recurse -File -Filter "*.md" | Get-Content -Raw -Encoding utf8) -join [Environment]::NewLine
foreach ($prefix in @("EPIC", "FEATURE", "UC", "RULE", "API", "EVENT", "TEST", "ADR")) {
    if ($all -notmatch "\b$prefix-(?:[A-Z]+-)?\d{4}\b") { throw "Traceability prefix has no valid identifier: $prefix" }
}

$requiredFiles = @("overview.md", "use-cases.md", "business-rules.md", "api-contracts.md", "events.md", "test-plan.md")
$activeModules = @("platform", "tenancy", "security-audit", "layout", "master-data", "inventory", "inbound", "outbound", "task-execution", "integration", "mobile-sync")
foreach ($module in $activeModules) {
    foreach ($file in $requiredFiles) {
        $path = Join-Path $docs "modules/$module/$file"
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing traceability source: $path" }
    }
}

$canonical = Get-Content -Raw -Encoding utf8 (Join-Path $docs "integration/canonical-contracts.md")
foreach ($api in @("API-INT-0001", "API-INT-0002", "API-MOB-0001", "API-MOB-0002", "API-MOB-0003")) {
    if (-not $canonical.Contains($api)) { throw "Canonical contract lacks $api" }
}
Write-Output "Traceability validation passed."
