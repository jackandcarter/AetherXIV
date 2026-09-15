param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$ServerRid = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root "bin\build\$Configuration\Windows-Core"
$staging = "$out.staging"
$dotnet = if ($env:DOTNET_BIN) { $env:DOTNET_BIN } else { 'dotnet' }
if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is required; run tools/Windows/install-build-dependencies.ps1.' }
if (-not ((& $dotnet --list-sdks) -match '^10\.0\.203\s')) { throw 'Pinned .NET SDK 10.0.203 is required.' }
$python = if (Get-Command python -ErrorAction SilentlyContinue) { 'python' } elseif (Get-Command py -ErrorAction SilentlyContinue) { 'py' } else { throw 'Python 3 is required.' }
Remove-Item -Recurse -Force $staging -ErrorAction Ignore
New-Item -ItemType Directory -Force $staging | Out-Null
try {
    & $python (Join-Path $root 'tools\Universal\create-direct-core-database-package.py') --repo-root $root --output-dir (Join-Path $staging 'Database')
    if ($LASTEXITCODE) { throw 'Database packaging failed.' }
    foreach ($entry in @(@('AetherXIV.Core.Map','map'), @('AetherXIV.Core.World','world'), @('AetherXIV.Core.Lobby','lobby'), @('AetherXIV.Launcher.Host','launcher-services'))) {
        & $dotnet publish (Join-Path $root "src\$($entry[0])\$($entry[0]).csproj") --configuration $Configuration --runtime $ServerRid --self-contained false --output (Join-Path $staging "servers\$($entry[1])") -m:1 /nodeReuse:false /p:NuGetAudit=false /p:UseAppHost=true
        if ($LASTEXITCODE) { throw "Publish failed: $($entry[0])" }
    }
    & $dotnet publish (Join-Path $root 'src\AetherXIV.UI.App\AetherXIV.UI.App.csproj') --configuration $Configuration --runtime $ServerRid --self-contained true --output (Join-Path $staging 'core\app') -m:1 /nodeReuse:false /p:NuGetAudit=false /p:UseAppHost=true
    if ($LASTEXITCODE) { throw 'Core app publish failed.' }
    & $python (Join-Path $root 'tools\Universal\lua-tree-manifest.py') --scripts-root (Join-Path $staging 'servers\map\scripts') --manifest (Join-Path $staging 'servers\map\scripts.manifest.json') --write
    if ($LASTEXITCODE) { throw 'Lua manifest generation failed.' }
    Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD_PARTY_NOTICES.md'), (Join-Path $root 'MODIFICATIONS.md'), (Join-Path $root 'TRADEMARKS.md') -Destination $staging
    $number = (Get-Content -Raw (Join-Path $root 'build-number.txt')).Trim()
    "schema=aetherxiv.build.manifest.v1`nproduct_version=2.1`nbuild_number=$number`nscope=core`nplatform=Windows" | Set-Content (Join-Path $staging 'build-manifest.txt')
    Remove-Item -Recurse -Force "$out.previous" -ErrorAction Ignore
    if (Test-Path $out) { Move-Item $out "$out.previous" }
    Move-Item $staging $out
    Remove-Item -Recurse -Force "$out.previous" -ErrorAction Ignore
    Write-Host "AetherXIV Windows core package: $out"
} finally { Remove-Item -Recurse -Force $staging -ErrorAction Ignore }
