[CmdletBinding()]
param([string]$FruityLinkSdkRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $buildProperties = @()
    $restoreOptions = @('--locked-mode')
    if ($FruityLinkSdkRoot) {
        $sdkRoot = (Resolve-Path -LiteralPath $FruityLinkSdkRoot).Path
        $buildProperties = @("-p:FruityLinkSdkRoot=$sdkRoot", '-p:NuGetLockFilePath=obj/source-sdk.packages.lock.json', '-p:ShouldUnsetParentConfigurationAndPlatform=false')
        $restoreOptions = @()
        if (-not $env:FRUITYLINK_TEST_PYTHON_PACKAGE) { $env:FRUITYLINK_TEST_PYTHON_PACKAGE = Join-Path $sdkRoot 'python/src' }
    }
    dotnet restore FlMcp.slnx @restoreOptions @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Locked dependency restore failed.' }
    dotnet build FlMcp.slnx --configuration Release --no-restore -warnaserror @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Code quality gate failed.' }
    dotnet test FlMcp.slnx --configuration Release --no-build --no-restore @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
