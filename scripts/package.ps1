[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/fl-mcp-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $packageRoot) { throw 'Package directory already exists. Choose a new directory.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
Push-Location $repoRoot
try {
    dotnet publish src/FlMcp.Server --configuration Release --no-self-contained -o (Join-Path $packageRoot 'server')
    if ($LASTEXITCODE -ne 0) { throw 'Server publish failed.' }
    dotnet build src/FlMcp.Plugin --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    $pluginOutput = Join-Path $repoRoot 'src/FlMcp.Plugin/bin/Release/net9.0-windows'
    $pluginTarget = Join-Path $packageRoot 'plugin/fl-mcp'
    New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null
    foreach ($name in @('FlMcp.Plugin.dll', 'FlMcp.Plugin.deps.json', 'FlMcp.Protocol.dll')) {
        Copy-Item -LiteralPath (Join-Path $pluginOutput $name) -Destination $pluginTarget
    }
    foreach ($name in @('LICENSE', 'README.md', 'THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $packageRoot
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'examples') -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $packageRoot -Recurse
    Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Get-FileHash -Algorithm SHA256 |
        Select-Object @{Name='File';Expression={[IO.Path]::GetRelativePath($packageRoot, $_.Path)}}, Hash |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.json') -Encoding utf8
    Write-Output $packageRoot
} finally { Pop-Location }
