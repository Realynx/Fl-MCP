[CmdletBinding()]
param([string]$OutputDirectory, [Parameter(Mandatory)][string]$FruityLinkSdkRoot, [string]$PythonWheel, [switch]$UsePrebuiltWheel, [string]$PythonExecutable = 'python')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = (Resolve-Path -LiteralPath $FruityLinkSdkRoot).Path
$wheelName = 'fruitylink_python-0.2.0-py3-none-any.whl'
if (-not (Test-Path -LiteralPath (Join-Path $sdkRoot 'src/FruityLink.Scripting/FruityLink.Scripting.csproj'))) { throw 'FruityLinkSdkRoot must contain the 0.2.0 scripting SDK source.' }
# The wheel file name carries the version, and the version never changes between SDK batches, so a
# wheel built from older sources installs, imports and reports 0.2.0 while silently missing whatever
# the newer sources added (2026-09-17: the shipped wheel was 21 modules stale and 12 modules short).
# Build it here from $sdkRoot/python and content-verify the staged copy against src/fruitylink.
$pythonSourceRoot = Join-Path $sdkRoot 'python'
if (-not (Test-Path -LiteralPath (Join-Path $pythonSourceRoot 'src/fruitylink') -PathType Container)) { throw 'FruityLinkSdkRoot must contain the fruitylink-python sources at python/src/fruitylink.' }
$wheelChecker = Join-Path $sdkRoot 'scripts/check_installed_wheels.py'
if (-not (Test-Path -LiteralPath $wheelChecker -PathType Leaf)) { throw "FruityLinkSdkRoot must contain the wheel content checker: $wheelChecker" }
if ($UsePrebuiltWheel) {
    if (-not $PythonWheel) { throw '-UsePrebuiltWheel requires -PythonWheel: supply the reviewed wheel to stage.' }
    $wheelPath = (Resolve-Path -LiteralPath $PythonWheel).Path
    if ([IO.Path]::GetFileName($wheelPath) -ne $wheelName) { throw 'Expected the SDK fruitylink-python 0.2.0 wheel.' }
} elseif ($PythonWheel) {
    Write-Warning "Ignoring -PythonWheel '$PythonWheel': the wheel is rebuilt from $pythonSourceRoot. Pass -UsePrebuiltWheel to stage the supplied file instead."
}
$buildProperties = @("-p:FruityLinkSdkRoot=$sdkRoot", '-p:NuGetLockFilePath=obj/source-sdk.packages.lock.json', '-p:ShouldUnsetParentConfigurationAndPlatform=false')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/fl-mcp-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $packageRoot) { throw 'Package directory already exists. Choose a new directory.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
$pythonTarget = Join-Path $packageRoot 'python'
New-Item -ItemType Directory -Path $pythonTarget | Out-Null
if ($UsePrebuiltWheel) {
    Copy-Item -LiteralPath $wheelPath -Destination $pythonTarget
} else {
    Push-Location $pythonSourceRoot
    try {
        & $PythonExecutable -m pip wheel . --no-deps -w $pythonTarget | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'SDK fruitylink-python wheel build failed.' }
    } finally { Pop-Location }
}
$staged = @(Get-ChildItem -LiteralPath $pythonTarget -Filter '*.whl' -File)
if ($staged.Count -ne 1 -or $staged[0].Name -ne $wheelName) { throw "Expected exactly one staged wheel named $wheelName; got: $($staged.Name -join ', ')" }
$stagedWheel = $staged[0].FullName
Add-Type -AssemblyName System.IO.Compression.FileSystem
$wheelArchive = [IO.Compression.ZipFile]::OpenRead($stagedWheel)
try {
    if (-not $wheelArchive.GetEntry('fruitylink/embedding.py')) { throw 'Rebuild the SDK wheel: embedded Python support is missing.' }
} finally { $wheelArchive.Dispose() }
# Every fruitylink/* file in the staged wheel must match python/src/fruitylink, and none may be absent.
& $PythonExecutable $wheelChecker $pythonTarget | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Staged wheel does not match $pythonSourceRoot/src/fruitylink; see the differences above." }
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
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'python/README.md') -Destination (Join-Path $pythonTarget 'README.md')
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'python/LICENSE') -Destination (Join-Path $packageRoot 'licenses/fruitylink-python-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'LICENSE') -Destination (Join-Path $packageRoot 'licenses/fruitylink-sdk-LICENSE.txt')
    Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Get-FileHash -Algorithm SHA256 |
        Select-Object @{Name='File';Expression={[IO.Path]::GetRelativePath($packageRoot, $_.Path)}}, Hash |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.json') -Encoding utf8
    Write-Output $packageRoot
} finally { Pop-Location }
