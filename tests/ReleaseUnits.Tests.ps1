[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\scripts\ReleaseUnits.psm1') -Force

function Assert-Equal { param($Expected, $Actual); if ($Expected -cne $Actual) { throw "Expected '$Expected', received '$Actual'." } }
function Assert-Throws { param([scriptblock]$Action); try { & $Action } catch { return }; throw 'Expected an exception.' }

$release = Resolve-HmProviderReleaseTag -Tag 'console-v1.2.3-preview.4'
Assert-Equal 'console' $release.ReleaseUnit
Assert-Equal '1.2.3-preview.4' $release.ReleaseVersion
Assert-Equal 'src/Hm.Logging.Providers.Console/Hm.Logging.Providers.Console.csproj' $release.Project
Assert-Equal 'HDev.Hm.Logging.Providers.Console' $release.PackageId
Assert-Equal $false $release.PublicationEnabled

foreach ($tag in @('files-v1.0.0', 'elasticsearch-v1.0.0', 'entityframework-v1.0.0', 'unknown-v1.0.0', 'console-v1.0', 'console-v01.0.0', 'console-v1.0.0-01', 'console-v1.0.0+build', 'Console-v1.0.0', 'v1.0.0')) {
    Assert-Throws { Resolve-HmProviderReleaseTag -Tag $tag }
}
Assert-Throws { Resolve-HmProviderReleaseTag -Tag 'console-v1.0.0' -RequirePublicationEnabled }
Assert-Throws { Get-HmProviderReleaseUnit -Name 'Console' }
Write-Output 'Release-unit tests passed.'
