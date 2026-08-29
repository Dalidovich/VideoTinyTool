param(
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [Parameter(Mandatory = $true)][string]$ToolsDir
)

$ErrorActionPreference = 'Stop'

$target = Join-Path $OutputDir 'ffprobe.exe'
if (Test-Path $target) { exit 0 }

$candidate = Join-Path $ToolsDir 'ffprobe.exe'
if (-not (Test-Path $candidate)) {
    $onPath = Get-Command ffprobe.exe -ErrorAction SilentlyContinue
    if ($null -ne $onPath) { $candidate = $onPath.Source }
}

if (Test-Path $candidate) {
    Copy-Item -LiteralPath $candidate -Destination $target -Force
    exit 0
}

Write-Warning "ffprobe.exe was not found. Put it in $ToolsDir or on PATH; the application needs it next to the executable."
exit 0
