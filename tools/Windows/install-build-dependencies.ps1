param([switch]$WhatIf)
$ErrorActionPreference = 'Stop'

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw 'winget is required to provision a Windows build host. Install App Installer, then rerun this script.'
}
function Install-Package([string]$Id) {
    $args = @('install', '--id', $Id, '--exact', '--accept-package-agreements', '--accept-source-agreements')
    if ($WhatIf) { Write-Host "winget $($args -join ' ')"; return }
    & winget @args
    if ($LASTEXITCODE -ne 0) { throw "winget failed to install $Id" }
}

Install-Package 'Microsoft.DotNet.SDK.10'
Install-Package 'Python.Python.3.13'
Install-Package 'Microsoft.VisualStudio.2022.BuildTools'
Install-Package 'MSYS2.MSYS2'
Write-Host 'Install the Visual Studio C++ workload and MSYS2 mingw-w64-i686-gcc if they are not already present, then rerun the build for verification.'
