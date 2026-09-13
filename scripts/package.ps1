[CmdletBinding()]
param([string]$OutputDirectory, [Parameter(Mandatory)][string]$FruityLinkSdkRoot, [Parameter(Mandatory)][string]$PythonWheel)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = (Resolve-Path -LiteralPath $FruityLinkSdkRoot).Path
$wheelPath = (Resolve-Path -LiteralPath $PythonWheel).Path
if ([IO.Path]::GetFileName($wheelPath) -ne 'fruitylink_python-0.2.0-py3-none-any.whl') { throw 'Expected the SDK fruitylink-python 0.2.0 wheel.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$wheelArchive = [IO.Compression.ZipFile]::OpenRead($wheelPath)
try {
    if (-not $wheelArchive.GetEntry('fruitylink/embedding.py')) { throw 'Rebuild the SDK wheel: embedded Python support is missing.' }
} finally { $wheelArchive.Dispose() }
if (-not (Test-Path -LiteralPath (Join-Path $sdkRoot 'src/FruityLink.Scripting/FruityLink.Scripting.csproj'))) { throw 'FruityLinkSdkRoot must contain the 0.2.0 scripting SDK source.' }
$buildProperties = @("-p:FruityLinkSdkRoot=$sdkRoot", '-p:NuGetLockFilePath=obj/source-sdk.packages.lock.json', '-p:ShouldUnsetParentConfigurationAndPlatform=false')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/fl-mcp-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $packageRoot) { throw 'Package directory already exists. Choose a new directory.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
Push-Location $repoRoot
try {
    dotnet publish src/FlMcp.Server --configuration Release --no-self-contained -o (Join-Path $packageRoot 'server') @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Server publish failed.' }
    dotnet build src/FlMcp.Plugin --configuration Release -warnaserror @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    $pluginOutput = Join-Path $repoRoot 'src/FlMcp.Plugin/bin/Release/net9.0-windows'
    $pluginTarget = Join-Path $packageRoot 'plugin/fl-mcp'
    New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null
    foreach ($name in @('FlMcp.Plugin.dll', 'FlMcp.Plugin.deps.json', 'FlMcp.Protocol.dll', 'FruityLink.Scripting.dll')) {
        Copy-Item -LiteralPath (Join-Path $pluginOutput $name) -Destination $pluginTarget
    }
    foreach ($name in @('LICENSE', 'README.md', 'THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $packageRoot
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'examples') -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'assets') -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $packageRoot -Recurse
    $pythonTarget = Join-Path $packageRoot 'python'
    New-Item -ItemType Directory -Path $pythonTarget | Out-Null
    Copy-Item -LiteralPath $wheelPath -Destination $pythonTarget
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'python/README.md') -Destination (Join-Path $pythonTarget 'README.md')
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'python/LICENSE') -Destination (Join-Path $packageRoot 'licenses/fruitylink-python-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'LICENSE') -Destination (Join-Path $packageRoot 'licenses/fruitylink-sdk-LICENSE.txt')
    Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Get-FileHash -Algorithm SHA256 |
        Select-Object @{Name='File';Expression={[IO.Path]::GetRelativePath($packageRoot, $_.Path)}}, Hash |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.json') -Encoding utf8
    Write-Output $packageRoot
} finally { Pop-Location }
