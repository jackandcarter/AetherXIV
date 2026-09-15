[CmdletBinding()]
param(
    [Parameter(Mandatory, ParameterSetName = 'Id')]
    [UInt32]$AccountId,
    [Parameter(Mandatory, ParameterSetName = 'Name')]
    [string]$AccountName,
    [switch]$Apply,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
if ($Apply -and -not $Yes) { throw 'Refusing mutation without -Yes.' }

$script = Join-Path $PSScriptRoot 'reset-account-characters.sh'
if (-not (Test-Path $script)) { throw "Missing shared reset tool: $script" }

$bash = Get-Command bash -ErrorAction SilentlyContinue
if ($null -eq $bash) {
    throw 'bash is required for the shared database reset tool. Install Git for Windows or run the same command from WSL.'
}

$arguments = @($script)
if ($PSCmdlet.ParameterSetName -eq 'Id') { $arguments += @('--account-id', $AccountId) }
else { $arguments += @('--account-name', $AccountName) }
if ($Apply) { $arguments += '--apply' }
if ($Yes) { $arguments += '--yes' }

& $bash.Source @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
