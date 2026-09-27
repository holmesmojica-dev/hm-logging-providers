[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseVersion)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force
Assert-HmProviderReleaseVersion -ReleaseVersion $ReleaseVersion
