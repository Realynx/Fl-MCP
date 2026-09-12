[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet restore FlMcp.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked dependency restore failed.' }
    dotnet build FlMcp.slnx --configuration Release --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Code quality gate failed.' }
    dotnet test FlMcp.slnx --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
