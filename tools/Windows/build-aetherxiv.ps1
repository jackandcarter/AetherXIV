param(
    [string]$Configuration = $(if ($env:AETHERXIV_BUILD_CONFIGURATION) { $env:AETHERXIV_BUILD_CONFIGURATION } else { "Release" }),
    [string]$ServerRid = $(if ($env:AETHERXIV_SERVER_RID) { $env:AETHERXIV_SERVER_RID } else { "win-x64" }),
    [string]$LauncherRid = $(if ($env:AETHERXIV_LAUNCHER_RID) { $env:AETHERXIV_LAUNCHER_RID } else { "win-x64" }),
    [ValidateSet('core', 'full')][string]$Scope = 'full',
    [switch]$InstallDependencies
)

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if ($InstallDependencies) {
    & (Join-Path $PSScriptRoot 'install-build-dependencies.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
}
if ($Scope -eq 'core') {
    & (Join-Path $PSScriptRoot 'build-core-only.ps1') -Configuration $Configuration -ServerRid $ServerRid
    exit $LASTEXITCODE
}
if ($Configuration -notin @("Debug", "Release")) { throw "Configuration must be Debug or Release." }
$FinalOutputRoot = Join-Path $rootDir "bin\build\$Configuration\Windows"
$OutputRoot = Join-Path $rootDir "bin\build\$Configuration\.Windows.staging"
$buildNumber = (Get-Content -Raw (Join-Path $rootDir "build-number.txt")).Trim()
$buildCompleted = $false

$dotnet = if ($env:DOTNET_BIN) { $env:DOTNET_BIN } else { "dotnet" }
$python = $null
$pythonPrefixArgs = @()
if ($env:PYTHON_BIN) {
    $python = $env:PYTHON_BIN
}
else {
    foreach ($candidate in @("python3", "python", "py")) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            $python = $command.Source
            if ($candidate -eq "py") { $pythonPrefixArgs = @("-3") }
            break
        }
    }
}
$launcherRoot = Join-Path $rootDir "AetherXIV Launcher"
$umbraVersion = if ($env:AETHERXIV_UMBRA_VERSION) { $env:AETHERXIV_UMBRA_VERSION } else { "2.1.0" }
$releaseWorkRoot = Join-Path $rootDir "bin\build\.work\$Configuration\Windows"
$env:AetherXivWorkRoot = $releaseWorkRoot

function Publish-Project {
    param(
        [string]$ProjectPath,
        [string]$OutputPath,
        [string[]]$ExtraArgs = @(),
        [switch]$SelfContained
    )

    $selfContainedValue = if ($SelfContained) { "true" } else { "false" }
    $symbolArgs = if ($Configuration -eq "Release") {
        @("/p:DebugType=None", "/p:DebugSymbols=false")
    }
    else {
        @()
    }
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    & $dotnet publish $ProjectPath `
        --configuration $Configuration `
        --self-contained $selfContainedValue `
        --output $OutputPath `
        -m:1 `
        /nodeReuse:false `
        /p:NuGetAudit=false `
        /p:PublishSingleFile=false `
        /p:UseAppHost=true `
        @symbolArgs `
        @ExtraArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed: $ProjectPath"
    }
}

function Reset-OutputRoot {
    $binRoot = Join-Path $rootDir "bin"
    $buildRoot = Join-Path $binRoot "build"
    New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
    # Build into an isolated sibling. The last verified release remains intact
    # until this package has passed every check.
    if (Test-Path $OutputRoot) { Remove-Item -Recurse -Force $OutputRoot }
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
}

function Resolve-MSBuild {
    if ($env:MSBUILD_EXE -and (Test-Path $env:MSBUILD_EXE)) {
        return $env:MSBUILD_EXE
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path $vswhere) {
            $candidate = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
            if ($candidate) {
                return $candidate
            }
        }
    }

    return $null
}

function Assert-BuildPrerequisites {
    $missing = @()
    $dotnetCommand = Get-Command $dotnet -ErrorAction SilentlyContinue
    $hasPinnedSdk = $false
    if ($dotnetCommand) {
        $hasPinnedSdk = @(& $dotnet --list-sdks 2>$null) -match '^10\.0\.203\s'
    }
    if (-not $hasPinnedSdk) {
        $missing += ".NET SDK 10.0.203 (dotnet)"
    }
    if (-not $python) {
        $missing += "Python 3"
    }
    if (-not (Resolve-MSBuild) -and -not (Get-Command "i686-w64-mingw32-g++" -ErrorAction SilentlyContinue)) {
        $missing += "Visual Studio C++ Build Tools or MinGW-w64"
    }

    if ($missing.Count -gt 0) {
        throw "AetherXIV Windows build prerequisites are missing:`n  - $($missing -join "`n  - ")`nSee docs/build/WINDOWS.md before running this build again."
    }
}

function Build-NativeUmbraWithMinGw {
    $mingw = Get-Command "i686-w64-mingw32-g++" -ErrorAction SilentlyContinue
    if (-not $mingw) {
        throw "A full Windows release requires Visual Studio C++ MSBuild or i686-w64-mingw32-g++."
    }

    $nativeRoot = Join-Path $OutputRoot "native"
    $injectorPath = Join-Path $nativeRoot "Umbra.NativeInjector.x86.exe"
    $bootstrapPath = Join-Path $nativeRoot "Aether.Umbra.Bootstrap.x86.dll"
    New-Item -ItemType Directory -Force -Path $nativeRoot | Out-Null

    & $mingw.Source -std=c++20 -O2 -municode -static `
        (Join-Path $launcherRoot "AetherXIV.Launcher.NativeInjector\umbra_native_injector.cpp") `
        -o $injectorPath
    if ($LASTEXITCODE -ne 0) { throw "MinGW native injector build failed." }

    $imgui = Join-Path $launcherRoot "Umbra\vendor\imgui"
    $bootstrap = Join-Path $launcherRoot "Umbra\Aether.Umbra.Bootstrap"
    & $mingw.Source -std=c++20 -O2 -fno-builtin -fno-tree-loop-distribute-patterns `
        -fno-exceptions -fno-rtti -DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD `
        "-I$imgui" "-I$(Join-Path $imgui 'backends')" -shared -static -static-libgcc -static-libstdc++ `
        '-Wl,--kill-at' -o $bootstrapPath `
        (Join-Path $bootstrap "dllmain.cpp") `
        (Join-Path $imgui "imgui.cpp") `
        (Join-Path $imgui "imgui_draw.cpp") `
        (Join-Path $imgui "imgui_tables.cpp") `
        (Join-Path $imgui "imgui_widgets.cpp") `
        (Join-Path $imgui "backends\imgui_impl_dx9.cpp") `
        (Join-Path $imgui "backends\imgui_impl_win32.cpp") `
        -lgdi32 -ldwmapi -lws2_32
    if ($LASTEXITCODE -ne 0) { throw "MinGW Umbra bootstrap build failed." }

    Copy-NativeUmbraPayloads $injectorPath $bootstrapPath
    Remove-Item -Recurse -Force $nativeRoot
}

function Copy-NativeUmbraPayloads {
    param(
        [string]$NativeInjectorPath,
        [string]$BootstrapPath
    )

    foreach ($helperRid in @("win-x64", "win-x86")) {
        $helperRoot = Join-Path $OutputRoot "launcher\app\Helpers\$helperRid"
        if (Test-Path $helperRoot) {
            Copy-Item -Force $NativeInjectorPath (Join-Path $helperRoot "Umbra.NativeInjector.x86.exe")
        }
    }

    $frameworkRoot = Join-Path $OutputRoot "launcher\app\Umbra\Framework"
    New-Item -ItemType Directory -Force -Path $frameworkRoot | Out-Null
    Copy-Item -Force $BootstrapPath (Join-Path $frameworkRoot "Aether.Umbra.Bootstrap.x86.dll")
    $assetsRoot = Join-Path $frameworkRoot "Assets"
    if (Test-Path $assetsRoot) {
        Remove-Item -Recurse -Force $assetsRoot
    }
    Copy-Item -Recurse -Force (Join-Path $launcherRoot "Umbra\assets") $assetsRoot
    Set-Content -Path (Join-Path $frameworkRoot "version.txt") -Value $umbraVersion
}

try {
Assert-BuildPrerequisites
Reset-OutputRoot

# Validate all non-build release inputs before spending time publishing binaries.
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\create-direct-core-database-package.py") `
    --repo-root $rootDir `
    --output-dir (Join-Path $OutputRoot "Database")
if ($LASTEXITCODE -ne 0) { throw "Database package creation failed." }

Write-Host "Publishing server hosts..."
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.Map\AetherXIV.Core.Map.csproj") (Join-Path $OutputRoot "servers\map") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.World\AetherXIV.Core.World.csproj") (Join-Path $OutputRoot "servers\world") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.Lobby\AetherXIV.Core.Lobby.csproj") (Join-Path $OutputRoot "servers\lobby") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Launcher.Host\AetherXIV.Launcher.Host.csproj") (Join-Path $OutputRoot "servers\launcher-services") @("--runtime", $ServerRid)
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\lua-tree-manifest.py") `
    --scripts-root (Join-Path $OutputRoot "servers\map\scripts") `
    --manifest (Join-Path $OutputRoot "servers\map\scripts.manifest.json") `
    --write
if ($LASTEXITCODE -ne 0) { throw "Lua inventory generation failed." }

Write-Host "Publishing launcher app and helpers..."
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.App\AetherXIV.Launcher.App.csproj") (Join-Path $OutputRoot "launcher\app") @("--runtime", $LauncherRid) -SelfContained
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.ClientLauncher\AetherXIV.Launcher.ClientLauncher.csproj") (Join-Path $OutputRoot "launcher\app\Helpers\win-x64") @("--runtime", "win-x64", "/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true") -SelfContained
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.ClientLauncher\AetherXIV.Launcher.ClientLauncher.csproj") (Join-Path $OutputRoot "launcher\app\Helpers\win-x86") @("--runtime", "win-x86", "/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true") -SelfContained

Write-Host "Publishing AetherXIV Core app..."
Publish-Project (Join-Path $rootDir "src\AetherXIV.UI.App\AetherXIV.UI.App.csproj") (Join-Path $OutputRoot "core\app") @("--runtime", $LauncherRid) -SelfContained

Write-Host "Publishing bundled Umbra base framework and private .NET runtime..."
Publish-Project `
    (Join-Path $launcherRoot "Umbra\Aether.Umbra.Framework\Aether.Umbra.Framework.csproj") `
    (Join-Path $OutputRoot "launcher\app\Umbra\Framework\Managed") `
    @("--runtime", "win-x86")
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\stage-umbra-dotnet-runtime.py") `
    --managed (Join-Path $OutputRoot "launcher\app\Umbra\Framework\Managed") `
    --runtime (Join-Path $OutputRoot "launcher\app\Umbra\Framework\Runtime") `
    --dotnet $dotnet
if ($LASTEXITCODE -ne 0) { throw "Private Umbra .NET runtime staging failed." }

$msbuild = Resolve-MSBuild
if ($msbuild) {
    Write-Host "Building native injector and bundled Umbra bootstrap with MSBuild..."
    $nativeRoot = Join-Path $OutputRoot "native"
    $nativeInjectorOutput = Join-Path $nativeRoot "injector"
    $bootstrapOutput = Join-Path $nativeRoot "umbra-bootstrap"
    New-Item -ItemType Directory -Force -Path $nativeInjectorOutput, $bootstrapOutput | Out-Null

    & $msbuild (Join-Path $launcherRoot "AetherXIV.Launcher.NativeInjector\AetherXIV.Launcher.NativeInjector.vcxproj") /p:Configuration=$Configuration /p:Platform=Win32 /m:1 "/p:OutDir=$nativeInjectorOutput\" /p:TargetName=Umbra.NativeInjector.x86
    if ($LASTEXITCODE -ne 0) { throw "MSBuild native injector build failed." }
    & $msbuild (Join-Path $launcherRoot "Umbra\Aether.Umbra.Bootstrap\Aether.Umbra.Bootstrap.vcxproj") /p:Configuration=$Configuration /p:Platform=Win32 /m:1 "/p:OutDir=$bootstrapOutput\" /p:TargetName=Aether.Umbra.Bootstrap.x86
    if ($LASTEXITCODE -ne 0) { throw "MSBuild Umbra bootstrap build failed." }
    Copy-NativeUmbraPayloads `
        (Join-Path $nativeInjectorOutput "Umbra.NativeInjector.x86.exe") `
        (Join-Path $bootstrapOutput "Aether.Umbra.Bootstrap.x86.dll")
    Remove-Item -Recurse -Force $nativeRoot
}
else {
    Write-Host "Visual Studio C++ MSBuild not found; building native Windows payloads with MinGW..."
    Build-NativeUmbraWithMinGw
}

$mapCorePath = Join-Path $OutputRoot "servers\map\AetherXIV.Core.Map.dll"
$mapCoreHash = (Get-FileHash -Algorithm SHA256 $mapCorePath).Hash.ToLowerInvariant()
@(
    "schema=aetherxiv.build.manifest.v1"
    "built_at_utc=$([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))"
    "configuration=$Configuration"
    "product_version=2.1"
    "build_number=$buildNumber"
    "server_rid=$ServerRid"
    "map_core_sha256=$mapCoreHash"
    "map_core_path=servers/map/AetherXIV.Core.Map.dll"
) | Set-Content -Encoding utf8 (Join-Path $OutputRoot "build-manifest.txt")

if ($Configuration -eq "Release") {
    Get-ChildItem -Path $OutputRoot -Recurse -File -Filter *.pdb | Remove-Item -Force
}
& $dotnet run `
    --project (Join-Path $launcherRoot "AetherXIV.Umbra.BundleFetcher\AetherXIV.Umbra.BundleFetcher.csproj") `
    --configuration $Configuration `
    -- `
    --stamp-local (Join-Path $OutputRoot "launcher\app\Umbra\Framework") $umbraVersion
if ($LASTEXITCODE -ne 0) { throw "Bundled Umbra framework integrity stamping failed." }
Write-Host "Packaging built-in Umbra plugins..."
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\package-bundled-plugins.py") `
    --output (Join-Path $OutputRoot "launcher\app\Umbra\BundledPlugins") `
    --dotnet $dotnet
if ($LASTEXITCODE -ne 0) { throw "Bundled Umbra plugin packaging failed." }
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\verify-umbra-bundle.py") `
    (Join-Path $OutputRoot "launcher\app\Umbra\Framework")
if ($LASTEXITCODE -ne 0) { throw "Bundled Umbra framework receipt verification failed." }

Copy-Item -LiteralPath (Join-Path $rootDir "LICENSE") -Destination (Join-Path $OutputRoot "LICENSE") -Force
Copy-Item -LiteralPath (Join-Path $rootDir "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $OutputRoot "THIRD_PARTY_NOTICES.md") -Force
Copy-Item -LiteralPath (Join-Path $rootDir "MODIFICATIONS.md") -Destination (Join-Path $OutputRoot "MODIFICATIONS.md") -Force
Copy-Item -LiteralPath (Join-Path $rootDir "TRADEMARKS.md") -Destination (Join-Path $OutputRoot "TRADEMARKS.md") -Force
Get-ChildItem -Path $OutputRoot -Recurse -File -Filter .DS_Store | Remove-Item -Force
if (Test-Path $releaseWorkRoot) {
    Remove-Item -Recurse -Force $releaseWorkRoot
}
$releaseWorkParent = Split-Path $releaseWorkRoot -Parent
if ((Test-Path $releaseWorkParent) -and -not (Get-ChildItem -Force $releaseWorkParent)) { Remove-Item -Force $releaseWorkParent }
$workRoot = Split-Path $releaseWorkParent -Parent
if ((Test-Path $workRoot) -and -not (Get-ChildItem -Force $workRoot)) { Remove-Item -Force $workRoot }
$forbiddenReleaseFiles = @(Get-ChildItem -Path $OutputRoot -Recurse -File | Where-Object {
    $_.Name -match '(?i)(\.Tests?\.|AetherXIV\.(Map|World|Lobby)\.Host)'
})
if ($forbiddenReleaseFiles.Count -gt 0) {
    throw "Release contains test or superseded server files: $($forbiddenReleaseFiles.FullName -join ', ')"
}

$requiredReleaseFiles = @(
    "LICENSE",
    "THIRD_PARTY_NOTICES.md",
    "MODIFICATIONS.md",
    "TRADEMARKS.md",
    "build-manifest.txt",
    "servers\map\AetherXIV.Core.Map.exe",
    "servers\world\AetherXIV.Core.World.exe",
    "servers\lobby\AetherXIV.Core.Lobby.exe",
    "servers\launcher-services\AetherXIV.Launcher.Host.exe",
    "core\app\AetherXIV.Core.App.exe",
    "launcher\app\AetherXIV.Launcher.App.exe",
    "servers\map\scripts\player.lua",
    "servers\map\scripts\directors\AfterQuestWarpDirector.lua",
    "servers\map\scripts\directors\WeatherDirector.lua",
    "servers\map\staticactors.bin",
    "servers\map\scripts.manifest.json",
    "servers\map\navmesh\wil0Field01.snb",
    "servers\map\navmesh\SHARPNAV_LICENSE",
    "Database\ffxiv_server.sql",
    "Database\ffxiv_server.sql.sha256",
    "Database\baseline-history.sha256",
    "Database\baseline-manifest.json",
    "Database\setup.sh",
    "Database\setup.ps1",
    "Database\migrations\20260724_000027_correct_1x_player_baselines.sql",
    "Database\migrations\20260724_000028_social_state_persistence.sql",
    "Database\migrations\20260727_000029_separate_umbra_control_plane.sql",
    "Database\migrations\20260727_000030_native_actor_slots.sql",
    "Database\migrations\20260728_000031_private_area_spawn_contract.sql",
    "Database\migrations\20260802_000032_quest_runtime_contract.sql",
    "launcher\app\Helpers\win-x64\Umbra.NativeInjector.x86.exe",
    "launcher\app\Helpers\win-x86\Umbra.NativeInjector.x86.exe",
    "launcher\app\Umbra\Framework\Aether.Umbra.Bootstrap.x86.dll",
    "launcher\app\Umbra\Framework\Managed\Aether.Umbra.Framework.exe",
    "launcher\app\Umbra\Framework\umbra-framework.json"
)
foreach ($relativePath in $requiredReleaseFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot $relativePath) -PathType Leaf)) {
        throw "Windows release is missing required file: $relativePath"
    }
}
$buildManifest = Get-Content -Raw (Join-Path $OutputRoot "build-manifest.txt")
if ($buildManifest -notmatch '(?m)^product_version=2\.1\r?$' -or
    $buildManifest -notmatch "(?m)^build_number=$([regex]::Escape($buildNumber))\r?$") {
    throw "Windows release build manifest has the wrong product or build identity."
}
$databaseBaseline = Get-Content -Raw (Join-Path $OutputRoot "Database\ffxiv_server.sql")
if ($databaseBaseline -notmatch 'aetherxiv-direct-core-v2' -or
    $databaseBaseline -notmatch 'CREATE TABLE IF NOT EXISTS server_battlenpc_spawn_audit_pins') {
    throw "Windows release database baseline omits a required runtime compatibility contract."
}

$previousOutputRoot = "$FinalOutputRoot.previous"
if (Test-Path $previousOutputRoot) {
    Remove-Item -Recurse -Force $previousOutputRoot
}
if (Test-Path $FinalOutputRoot) {
    Move-Item -LiteralPath $FinalOutputRoot -Destination $previousOutputRoot
}
try {
    Move-Item -LiteralPath $OutputRoot -Destination $FinalOutputRoot
    $buildCompleted = $true
    if (Test-Path $previousOutputRoot) {
        Remove-Item -Recurse -Force $previousOutputRoot
    }
}
catch {
    if ((Test-Path $previousOutputRoot) -and -not (Test-Path $FinalOutputRoot)) {
        Move-Item -LiteralPath $previousOutputRoot -Destination $FinalOutputRoot
    }
    throw
}
Write-Host "AetherXIV Windows build complete."
Write-Host "Output: $FinalOutputRoot"
}
finally {
    if (Test-Path $releaseWorkRoot) {
        Remove-Item -Recurse -Force $releaseWorkRoot
    }
    if (-not $buildCompleted -and (Test-Path $OutputRoot)) {
        Remove-Item -Recurse -Force $OutputRoot
    }
}
