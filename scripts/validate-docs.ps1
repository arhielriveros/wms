[CmdletBinding()]
param([string]$DocsPath = "docs")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$docs = (Resolve-Path (Join-Path $root $DocsPath)).Path
$errors = [Collections.Generic.List[string]]::new()

foreach ($file in Get-ChildItem -LiteralPath $docs -Recurse -File -Filter "*.md") {
    $content = Get-Content -Raw -Encoding utf8 $file.FullName
    if (-not $content.EndsWith([Environment]::NewLine) -and -not $content.EndsWith([char]10)) {
        $errors.Add("$($file.FullName): missing final newline")
    }
    $pattern = "\[[^\]]+\]\((?!https?://|#|mailto:)(?<path>[^)#]+)(?:#[^)]+)?\)"
    foreach ($match in [regex]::Matches($content, $pattern)) {
        $relative = [Uri]::UnescapeDataString($match.Groups["path"].Value)
        $target = Join-Path $file.DirectoryName $relative
        if (-not (Test-Path -LiteralPath $target)) { $errors.Add("$($file.FullName): broken relative link '$relative'") }
    }
}
if ($errors.Count) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Output "Documentation validation passed."
